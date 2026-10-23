// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Katch.Backend.Infrastructure.Errors;

/// <summary>
/// Converte a <see cref="ValidationFailedException"/> lançada por um serviço na resposta <c>400</c> ERR-10.
/// </summary>
internal sealed class ValidationFailedExceptionFilter : IExceptionFilter
{
    /// <inheritdoc/>
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Exception is ValidationFailedException exception)
        {
            context.Result = new BadRequestObjectResult(ApiErrorResponse.ValidationFailed(exception.Fields));
            context.ExceptionHandled = true;
        }
    }
}
