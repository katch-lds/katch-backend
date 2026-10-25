// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Services;
using Microsoft.Extensions.Options;

namespace Katch.Backend.BackgroundJobs;

/// <summary>
/// Tarefa periódica que repõe na totalidade a quota dos Candidatos cujo período de bloqueio terminou, a cada
/// <see cref="BackgroundJobsOptions.IntervalSeconds"/> segundos, por omissão a cada minuto (RF021, P11; modelo de classes
/// do backend v02, secção 5.2).
/// </summary>
/// <remarks>
/// A tarefa não é a garantia da regra: a consulta da quota e a manifestação de interesse verificam o fim do bloqueio no
/// próprio pedido, por isso o valor devolvido ao Candidato está certo mesmo antes de a tarefa correr. Uma execução falhada
/// é registada e repetida no intervalo seguinte, e nunca pára a aplicação.
/// </remarks>
public sealed partial class QuotaRestoreWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly TimeSpan interval;
    private readonly ILogger<QuotaRestoreWorker> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuotaRestoreWorker"/> class.
    /// </summary>
    /// <param name="scopeFactory">Cria o âmbito de cada execução, porque os serviços de negócio têm vida por pedido.</param>
    /// <param name="options">Configuração das tarefas periódicas, já validada no arranque.</param>
    /// <param name="logger">Registo de execuções e de falhas.</param>
    public QuotaRestoreWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundJobsOptions> options,
        ILogger<QuotaRestoreWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.scopeFactory = scopeFactory;
        this.interval = TimeSpan.FromSeconds(options.Value.IntervalSeconds);
        this.logger = logger;
    }

    /// <summary>
    /// Executa uma reposição: abre um âmbito, repõe as quotas vencidas e regista o resultado. Qualquer falha que não seja
    /// o cancelamento é registada e engolida, para que a tarefa continue no intervalo seguinte.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da tarefa, quando a aplicação pára.</param>
    /// <returns>Tarefa que termina quando a execução acabou.</returns>
    internal async Task RestoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = this.scopeFactory.CreateScope();
            var quotaService = scope.ServiceProvider.GetRequiredService<IQuotaService>();

            int restored = await quotaService.RestoreDueQuotasAsync(cancellationToken);

            if (restored > 0)
            {
                this.LogRestored(restored);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Uma falha (base de dados indisponível, conflito de concorrência com um interesse no mesmo instante) não pode
            // parar a aplicação: o .NET pára o servidor se uma tarefa de fundo terminar com exceção.
            this.LogFailed(exception);
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(this.interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await this.RestoreAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A aplicação está a parar.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Quotas de interesses repostas: {Count}.")]
    private partial void LogRestored(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "A reposição das quotas de interesses falhou; repete no intervalo seguinte.")]
    private partial void LogFailed(Exception exception);
}
