// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Errors;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Options;

namespace Katch.Backend.Infrastructure.Hosting;

/// <summary>
/// Recusa com o ERR-12 (413), antes de ler o corpo, um pedido que declare uma dimensão acima de
/// <see cref="LimitsOptions.MaxRequestBodyBytes"/>. Os carregamentos têm limite próprio (<c>RequestSizeLimit</c>) e
/// passam; os pedidos sem dimensão declarada ficam com o limite do Kestrel, configurado com o mesmo valor.
/// </summary>
internal sealed class RequestBodyLimitMiddleware
{
    private readonly RequestDelegate next;
    private readonly long maxRequestBodyBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestBodyLimitMiddleware"/> class.
    /// </summary>
    /// <param name="next">Passo seguinte do pipeline.</param>
    /// <param name="limits">Limites globais, validados no arranque.</param>
    public RequestBodyLimitMiddleware(RequestDelegate next, IOptions<LimitsOptions> limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        this.next = next;
        this.maxRequestBodyBytes = limits.Value.MaxRequestBodyBytes;
    }

    /// <summary>
    /// Responde 413 ERR-12 se a dimensão declarada passar do limite; caso contrário, segue o pipeline.
    /// </summary>
    /// <param name="context">Contexto do pedido.</param>
    /// <returns>Tarefa que termina quando o pedido foi tratado.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool hasOwnLimit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>() is not null;
        if (!hasOwnLimit && context.Request.ContentLength > this.maxRequestBodyBytes)
        {
            return ApiErrorResponse.WriteRequestTooLargeAsync(context);
        }

        return this.next(context);
    }
}
