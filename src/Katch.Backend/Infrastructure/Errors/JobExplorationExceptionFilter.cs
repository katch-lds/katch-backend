// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Katch.Backend.Infrastructure.Errors;

/// <summary>
/// Converte nas respostas do catálogo de erros as exceções da resposta do Candidato a uma vaga: a
/// <see cref="JobAnswerRejectedException"/> (<c>404</c> ERR-20, <c>409</c> ERR-32 e ERR-33), a
/// <see cref="QuotaExhaustedException"/> (<c>409</c> ERR-31) e a <see cref="ConcurrencyConflictException"/> (<c>409</c>
/// ERR-41).
/// </summary>
internal sealed class JobExplorationExceptionFilter : IExceptionFilter
{
    /// <inheritdoc/>
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IActionResult? result = context.Exception switch
        {
            JobAnswerRejectedException { Reason: JobAnswerRejection.NotFound } => new NotFoundObjectResult(ApiErrorResponse.NotFound()),
            JobAnswerRejectedException { Reason: JobAnswerRejection.AlreadyAnswered } => new ConflictObjectResult(ApiErrorResponse.AlreadyAnswered()),
            JobAnswerRejectedException { Reason: JobAnswerRejection.Unavailable } => new ConflictObjectResult(ApiErrorResponse.JobUnavailable()),
            QuotaExhaustedException exception => new ConflictObjectResult(
                ApiErrorResponse.QuotaExhausted(exception.BlockedUntil, exception.Remaining)),
            ConcurrencyConflictException => new ConflictObjectResult(ApiErrorResponse.ConcurrencyConflict()),
            _ => null,
        };

        if (result is not null)
        {
            context.Result = result;
            context.ExceptionHandled = true;
        }
    }
}
