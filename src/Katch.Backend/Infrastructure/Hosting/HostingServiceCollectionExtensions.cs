// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace Katch.Backend.Infrastructure.Hosting;

/// <summary>
/// Registo e pipeline da infraestrutura de alojamento: limites globais, cabeçalhos do proxy, limite do corpo dos pedidos
/// e verificações de saúde.
/// </summary>
public static class HostingServiceCollectionExtensions
{
    /// <summary>
    /// Configuração que liga a leitura de <c>X-Forwarded-For</c> e <c>X-Forwarded-Proto</c>, para usar só atrás do
    /// proxy do alojamento (variável de ambiente <c>ForwardedHeaders__Enabled</c>).
    /// </summary>
    public const string ForwardedHeadersEnabledKey = "ForwardedHeaders:Enabled";

    /// <summary>
    /// Rota da verificação de vida: a API responde.
    /// </summary>
    public const string LivenessRoute = "/health";

    /// <summary>
    /// Rota da verificação de prontidão: a API responde e liga à base de dados.
    /// </summary>
    public const string ReadinessRoute = "/health/ready";

    private const string ReadyTag = "ready";

    /// <summary>
    /// Regista os limites globais (validados no arranque), o limite do corpo no Kestrel, os cabeçalhos do proxy e as
    /// verificações de saúde.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <returns>A mesma coleção, para encadear chamadas.</returns>
    public static IServiceCollection AddKatchHosting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidateOptions<LimitsOptions>, LimitsOptionsValidator>();
        services.AddOptions<LimitsOptions>().BindConfiguration(LimitsOptions.SectionName).ValidateOnStart();
        services.AddOptions<KestrelServerOptions>()
            .Configure<IOptions<LimitsOptions>>((kestrel, limits) =>
                kestrel.Limits.MaxRequestBodySize = limits.Value.MaxRequestBodyBytes);

        // O TLS termina no proxy do alojamento, que acrescenta o IP do cliente no fim do X-Forwarded-For: só esse
        // último valor é aceite (ForwardLimit = 1). O endereço do proxy não é fixo, por isso não há lista de proxies;
        // a opção só se liga onde a API não é acessível sem passar pelo proxy.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);
        return services;
    }

    /// <summary>
    /// Lê o IP do cliente e o esquema dos cabeçalhos do proxy, quando <see cref="ForwardedHeadersEnabledKey"/> está
    /// ligado. Tem de ser o primeiro passo do pipeline.
    /// </summary>
    /// <param name="app">Aplicação.</param>
    /// <returns>A mesma aplicação, para encadear chamadas.</returns>
    public static WebApplication UseKatchForwardedHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Configuration.GetValue<bool>(ForwardedHeadersEnabledKey))
        {
            app.UseForwardedHeaders();
        }

        return app;
    }

    /// <summary>
    /// Acrescenta ao pipeline a recusa dos pedidos que declarem um corpo acima do limite global.
    /// </summary>
    /// <param name="app">Construtor do pipeline.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static IApplicationBuilder UseKatchRequestBodyLimit(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<RequestBodyLimitMiddleware>();
    }

    /// <summary>
    /// Mapeia as verificações de saúde, anónimas: <see cref="LivenessRoute"/> sem verificações e
    /// <see cref="ReadinessRoute"/> com a ligação à base de dados. Respondem 200 <c>Healthy</c> ou 503 <c>Unhealthy</c>.
    /// </summary>
    /// <param name="endpoints">Construtor das rotas.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static IEndpointRouteBuilder MapKatchHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks(LivenessRoute, new() { Predicate = _ => false }).AllowAnonymous();
        endpoints.MapHealthChecks(ReadinessRoute, new() { Predicate = check => check.Tags.Contains(ReadyTag) })
            .AllowAnonymous();
        return endpoints;
    }
}
