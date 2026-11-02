// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Katch.Backend.Infrastructure.Errors;

/// <summary>
/// Converte as exceções de estado lançadas pelos serviços nas respostas de erro do catálogo:
/// <see cref="ElementNotFoundException"/> em <c>404</c> ERR-20, <see cref="ElementForbiddenException"/> em <c>403</c>
/// ERR-08 e <see cref="OperationConflictException"/> em <c>409</c> com o erro que transporta.
/// </summary>
internal sealed class ServiceExceptionFilter : IExceptionFilter
{
    /// <inheritdoc/>
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        switch (context.Exception)
        {
            case ElementNotFoundException:
                context.Result = new NotFoundObjectResult(ApiErrorResponse.NotFound());
                context.ExceptionHandled = true;
                break;
            case ElementForbiddenException:
                context.Result = new ObjectResult(ApiErrorResponse.ElementForbidden())
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
                context.ExceptionHandled = true;
                break;
            case OperationConflictException conflict:
                context.Result = new ConflictObjectResult(conflict.Error);
                context.ExceptionHandled = true;
                break;
        }
    }
}
