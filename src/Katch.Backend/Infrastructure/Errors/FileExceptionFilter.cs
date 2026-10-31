// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Katch.Backend.Infrastructure.Errors;

/// <summary>
/// Converte os erros dos ficheiros enviados nas respostas do catálogo: a <see cref="FileRejectedException"/> em
/// <c>400</c> ERR-11 e o pedido que o servidor recusa por exceder o limite de dimensão em <c>413</c> ERR-12.
/// </summary>
internal sealed class FileExceptionFilter : IExceptionFilter
{
    /// <inheritdoc/>
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        switch (context.Exception)
        {
            case FileRejectedException rejected:
                context.Result = new BadRequestObjectResult(ApiErrorResponse.FileRejected(rejected.Reason));
                context.ExceptionHandled = true;
                break;
            case BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge }:
                context.Result = new ObjectResult(ApiErrorResponse.RequestTooLarge())
                {
                    StatusCode = StatusCodes.Status413PayloadTooLarge,
                };
                context.ExceptionHandled = true;
                break;
        }
    }
}
