// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Registo na injeção de dependências e no pipeline dos serviços de autenticação e autorização.
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

    /// <summary>
    /// Regista a validação da credencial JWT, as políticas de autorização e as respostas de erro 401 e 403.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <returns>A mesma coleção, para encadear chamadas.</returns>
    public static IServiceCollection AddKatchAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKatchJwtOptions();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.Candidate, policy => policy.RequireRole(AccountRoles.Candidate))
            .AddPolicy(AuthorizationPolicies.Recruiter, policy => policy.RequireRole(AccountRoles.Recruiter))
            .AddPolicy(
                AuthorizationPolicies.CandidateOrRecruiter,
                policy => policy.RequireRole(AccountRoles.Candidate, AccountRoles.Recruiter))
            .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(AccountRoles.Admin))
            .AddPolicy(
                AuthorizationPolicies.ApprovedCompany,
                policy => policy
                    .RequireRole(AccountRoles.Recruiter)
                    .AddRequirements(new ApprovedCompanyRequirement()));
        services.AddScoped<IAuthorizationHandler, ApprovedCompanyAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiErrorAuthorizationResultHandler>();
        return services;
    }

    /// <summary>
    /// Acrescenta ao pipeline a verificação do estado da conta. Tem de ficar depois de <c>UseAuthentication</c> e
    /// antes de <c>UseAuthorization</c>.
    /// </summary>
    /// <param name="app">Construtor do pipeline.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static IApplicationBuilder UseAccountStatusCheck(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<AccountStatusMiddleware>();
    }
}
