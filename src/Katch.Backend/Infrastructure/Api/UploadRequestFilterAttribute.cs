// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace Katch.Backend.Infrastructure.Api;

/// <summary>
/// Filtro dos endpoints de envio de ficheiros, que corre antes da leitura do corpo: um tipo de conteúdo que não seja
/// <c>multipart/form-data</c> dá o ERR-13 (415) e um pedido que declare uma dimensão acima do limite dá o ERR-12 (413).
/// O limite do servidor (<c>RequestSizeLimit</c>) cobre os pedidos sem dimensão declarada.
/// </summary>
/// <param name="maxRequestBytes">Dimensão máxima do pedido, em bytes.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class UploadRequestFilterAttribute(long maxRequestBytes) : Attribute, IResourceFilter
{
    /// <inheritdoc/>
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HttpRequest request = context.HttpContext.Request;
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out MediaTypeHeaderValue? mediaType)
            || !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(ApiErrorResponse.UnsupportedContentType())
            {
                StatusCode = StatusCodes.Status415UnsupportedMediaType,
            };
        }
        else if (request.ContentLength > maxRequestBytes)
        {
            context.Result = new ObjectResult(ApiErrorResponse.RequestTooLarge())
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge,
            };
        }
    }

    /// <inheritdoc/>
    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        // Nada a fazer depois da execução.
    }
}
