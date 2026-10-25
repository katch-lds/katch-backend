// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Katch.Backend.UnitTests.Infrastructure.Errors;

/// <summary>
/// Testes do filtro de exceções da resposta do Candidato a uma vaga: conversão nas respostas do catálogo de erros e
/// passagem das outras exceções.
/// </summary>
public sealed class JobExplorationExceptionFilterTests
{
    /// <summary>
    /// Cada motivo de rejeição da resposta é convertido no estado HTTP e no código do catálogo.
    /// </summary>
    /// <param name="reason">Motivo da rejeição.</param>
    /// <param name="status">Estado HTTP esperado.</param>
    /// <param name="code">Código do erro esperado.</param>
    [Theory]
    [InlineData(JobAnswerRejection.NotFound, 404, "ERR-20")]
    [InlineData(JobAnswerRejection.AlreadyAnswered, 409, "ERR-32")]
    [InlineData(JobAnswerRejection.Unavailable, 409, "ERR-33")]
    public void ConvertsJobAnswerRejections(JobAnswerRejection reason, int status, string code)
    {
        ExceptionContext context = Run(new JobAnswerRejectedException(reason));

        AssertResult(context, status, code);
    }

    /// <summary>
    /// A quota esgotada é convertida no ERR-31, com o tempo em falta e o <c>blockedUntil</c>.
    /// </summary>
    [Fact]
    public void ConvertsQuotaExhaustedToErr31()
    {
        var blockedUntil = new DateTimeOffset(2026, 10, 10, 16, 5, 0, TimeSpan.Zero);

        ExceptionContext context = Run(new QuotaExhaustedException(blockedUntil, new TimeSpan(23, 47, 0)));

        ApiErrorDto error = AssertResult(context, 409, "ERR-31");
        Assert.Equal(blockedUntil, error.BlockedUntil);
        Assert.Contains("23 h 47 min", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// O conflito de concorrência é convertido no ERR-41.
    /// </summary>
    [Fact]
    public void ConvertsConcurrencyConflictToErr41()
    {
        ExceptionContext context = Run(new ConcurrencyConflictException(new InvalidOperationException()));

        AssertResult(context, 409, "ERR-41");
    }

    /// <summary>
    /// As outras exceções passam sem serem tratadas.
    /// </summary>
    [Fact]
    public void LeavesOtherExceptionsUnhandled()
    {
        ExceptionContext context = Run(new InvalidOperationException("falha"));

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    private static ExceptionContext Run(Exception exception)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ExceptionContext(actionContext, []) { Exception = exception };
        new Katch.Backend.Infrastructure.Errors.JobExplorationExceptionFilter().OnException(context);
        return context;
    }

    private static ApiErrorDto AssertResult(ExceptionContext context, int status, string code)
    {
        Assert.True(context.ExceptionHandled);
        var result = Assert.IsAssignableFrom<ObjectResult>(context.Result);
        Assert.Equal(status, result.StatusCode);
        ApiErrorDto error = Assert.IsType<ApiErrorDto>(result.Value);
        Assert.Equal(code, error.Code);
        return error;
    }
}
