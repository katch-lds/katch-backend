// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Katch.Backend.UnitTests.Infrastructure;

/// <summary>
/// Testes de <see cref="LoginRejectedExceptionFilter"/>.
/// </summary>
public sealed class LoginRejectedExceptionFilterTests
{
    private readonly LoginRejectedExceptionFilter filter = new();

    /// <summary>
    /// A recusa do início de sessão passa a resposta de erro do catálogo, e a exceção fica tratada.
    /// </summary>
    /// <param name="reason">Motivo da recusa.</param>
    /// <param name="status">Estado HTTP esperado.</param>
    /// <param name="code">Código esperado.</param>
    [Theory]
    [InlineData(LoginRejection.InvalidCredentials, 401, "ERR-02")]
    [InlineData(LoginRejection.AccountBlocked, 403, "ERR-03")]
    [InlineData(LoginRejection.AccountSuspended, 403, "ERR-04")]
    [InlineData(LoginRejection.WrongAccessPoint, 403, "ERR-05")]
    public void LoginRejectedBecomesTheCatalogResponse(LoginRejection reason, int status, string code)
    {
        ExceptionContext context = NewContext(new LoginRejectedException(reason));

        this.filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        ObjectResult result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(code, Assert.IsType<ApiErrorDto>(result.Value).Code);
    }

    /// <summary>
    /// Qualquer outra exceção segue o seu caminho, sem ser tratada por este filtro.
    /// </summary>
    [Fact]
    public void OtherExceptionsAreLeftAlone()
    {
        ExceptionContext context = NewContext(new InvalidOperationException("outra"));

        this.filter.OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    /// <summary>
    /// Sem contexto, o filtro recusa o pedido em vez de ignorar o erro.
    /// </summary>
    [Fact]
    public void NullContextIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => this.filter.OnException(null!));
    }

    private static ExceptionContext NewContext(Exception exception)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, []) { Exception = exception };
    }
}
