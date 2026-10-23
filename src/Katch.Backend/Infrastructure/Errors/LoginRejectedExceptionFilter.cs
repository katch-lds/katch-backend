// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Katch.Backend.Infrastructure.Errors;

/// <summary>
/// Converte a <see cref="LoginRejectedException"/> lançada por um serviço na resposta <c>401</c> (ERR-02) ou <c>403</c>
/// (ERR-03, ERR-04 ou ERR-05).
/// </summary>
internal sealed class LoginRejectedExceptionFilter : IExceptionFilter
{
    /// <inheritdoc/>
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Exception is LoginRejectedException exception)
        {
            (int statusCode, var body) = ApiErrorResponse.LoginRejected(exception.Reason);
            context.Result = new ObjectResult(body) { StatusCode = statusCode };
            context.ExceptionHandled = true;
        }
    }
}
