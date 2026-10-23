// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Services;
using Microsoft.Extensions.Options;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Registo na injeção de dependências dos serviços de autenticação.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Regista a configuração JWT (validada no arranque), o relógio e o serviço de emissão de credenciais.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <returns>A mesma coleção, para encadear chamadas.</returns>
    public static IServiceCollection AddKatchJwtOptions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        return services;
    }
}
