// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Threading.RateLimiting;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Errors;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Katch.Backend.Infrastructure.RateLimiting;

/// <summary>
/// Limite de pedidos com o <c>RateLimiter</c> do ASP.NET Core, em janela fixa e sem fila: por IP do cliente nos
/// endpoints anónimos das <see cref="RateLimitPolicies"/> e por conta em todos os pedidos autenticados. Acima do limite,
/// a resposta é <c>429</c> ERR-14 com o cabeçalho <c>Retry-After</c>. O IP só é o do cliente com os cabeçalhos do
/// proxy ligados (<c>ForwardedHeaders__Enabled</c>).
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Regista as políticas e o limite global, com os valores de <see cref="LimitsOptions"/>.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <returns>A mesma coleção, para encadear chamadas.</returns>
    public static IServiceCollection AddKatchRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<LimitsOptions>>((options, limits) => Configure(options, limits.Value));
        return services;
    }

    private static void Configure(RateLimiterOptions options, LimitsOptions limits)
    {
        var window = TimeSpan.FromSeconds(limits.RateLimitWindowSeconds);

        options.AddPolicy(RateLimitPolicies.Login, context => PerClientIp(context, limits.LoginPermitLimit, window));
        options.AddPolicy(
            RateLimitPolicies.Registration,
            context => PerClientIp(context, limits.RegistrationPermitLimit, window));
        options.AddPolicy(RateLimitPolicies.Locations, context => PerClientIp(context, limits.LocationsPermitLimit, window));
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.User.TryGetAccountId(out Guid accountId)
                ? RateLimitPartition.GetFixedWindowLimiter(accountId.ToString(), _ => Window(limits.UserPermitLimit, window))
                : RateLimitPartition.GetNoLimiter(string.Empty));
        options.OnRejected = (context, _) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            return new ValueTask(ApiErrorResponse.WriteTooManyRequestsAsync(context.HttpContext));
        };
    }

    private static RateLimitPartition<string> PerClientIp(HttpContext context, int permitLimit, TimeSpan window)
    {
        string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => Window(permitLimit, window));
    }

    private static FixedWindowRateLimiterOptions Window(int permitLimit, TimeSpan window)
    {
        return new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 };
    }
}
