// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Services;
using Microsoft.Extensions.Options;

namespace Katch.Backend.BackgroundJobs;

/// <summary>
/// Tarefa periódica que encerra as vagas publicadas cuja data-limite foi atingida, a cada
/// <see cref="BackgroundJobsOptions.IntervalSeconds"/> segundos, por omissão a cada minuto (RF057; modelo de classes do
/// backend v02, secção 5.2, <c>JobExpirationWorker</c>).
/// </summary>
/// <remarks>
/// A tarefa não é a garantia de que uma vaga expirada deixa de ser apresentada: a exploração de vagas e o interesse
/// verificam a data-limite no próprio pedido (DM-17). Uma execução falhada é registada e repetida no intervalo seguinte,
/// e nunca pára a aplicação.
/// </remarks>
public sealed partial class JobExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly TimeSpan interval;
    private readonly ILogger<JobExpirationWorker> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExpirationWorker"/> class.
    /// </summary>
    /// <param name="scopeFactory">Cria o âmbito de cada execução, porque os serviços de negócio têm vida por pedido.</param>
    /// <param name="options">Configuração das tarefas periódicas, já validada no arranque.</param>
    /// <param name="logger">Registo de execuções e de falhas.</param>
    public JobExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundJobsOptions> options,
        ILogger<JobExpirationWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.scopeFactory = scopeFactory;
        this.interval = TimeSpan.FromSeconds(options.Value.IntervalSeconds);
        this.logger = logger;
    }

    /// <summary>
    /// Executa um encerramento: abre um âmbito, encerra as vagas expiradas e regista o resultado. Qualquer falha que não
    /// seja o cancelamento é registada e engolida, para que a tarefa continue no intervalo seguinte.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da tarefa, quando a aplicação pára.</param>
    /// <returns>Tarefa que termina quando a execução acabou.</returns>
    internal async Task CloseExpiredAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = this.scopeFactory.CreateScope();
            var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();

            int closed = await jobService.CloseExpiredAsync(cancellationToken);

            if (closed > 0)
            {
                this.LogClosed(closed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Uma falha (base de dados indisponível, por exemplo) não pode parar a aplicação: o .NET pára o servidor se
            // uma tarefa de fundo terminar com exceção. Nada fica gravado, porque a gravação é única.
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
                await this.CloseExpiredAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A aplicação está a parar.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Vagas encerradas pela data-limite: {Count}.")]
    private partial void LogClosed(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "O encerramento das vagas com a data-limite atingida falhou; repete no intervalo seguinte.")]
    private partial void LogFailed(Exception exception);
}
