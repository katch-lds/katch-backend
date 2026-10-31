// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.Extensions.Options;

namespace Katch.Backend.BackgroundJobs;

/// <summary>
/// Registo na injeção de dependências das tarefas periódicas. Cada Issue acrescenta aqui a sua.
/// </summary>
public static class BackgroundJobsServiceCollectionExtensions
{
    /// <summary>
    /// Regista a configuração das tarefas periódicas (validada no arranque), a reposição da quota de interesses e o
    /// encerramento das vagas com a data-limite atingida.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <returns>A mesma coleção, para encadear chamadas.</returns>
    public static IServiceCollection AddKatchBackgroundJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidateOptions<BackgroundJobsOptions>, BackgroundJobsOptionsValidator>();
        services.AddOptions<BackgroundJobsOptions>().BindConfiguration(BackgroundJobsOptions.SectionName).ValidateOnStart();
        services.AddHostedService<QuotaRestoreWorker>();
        services.AddHostedService<JobExpirationWorker>();
        return services;
    }
}
