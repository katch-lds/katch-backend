// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Registo na injeção de dependências dos repositórios e dos serviços de negócio (modelo de classes do backend v02,
/// secções 4 e 5). Cada Issue acrescenta aqui os seus.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Regista os repositórios e a unidade de trabalho por pedido, o cálculo do resumo das palavras-passe e os serviços.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <returns>A mesma coleção, para encadear chamadas.</returns>
    public static IServiceCollection AddKatchApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICandidateRepository, CandidateRepository>();
        services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IReferenceListService, ReferenceListService>();
        services.AddScoped<IQuotaService, QuotaService>();
        services.AddScoped<IJobExplorationService, JobExplorationService>();
        return services;
    }
}
