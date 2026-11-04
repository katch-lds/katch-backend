// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Katch.Backend.UnitTests.Infrastructure.Errors;

/// <summary>
/// Testes dos filtros que convertem as exceções dos serviços nas respostas do catálogo de erros (documentação da API
/// v02, secção 4): <see cref="ServiceExceptionFilter"/> (ERR-20, ERR-08 e conflitos), <see cref="FileExceptionFilter"/>
/// (ERR-11 e ERR-12) e <see cref="ValidationFailedExceptionFilter"/> (ERR-10).
/// </summary>
public sealed class ExceptionFiltersTests
{
    /// <summary>
    /// O elemento inexistente dá <c>404</c> ERR-20, o elemento de outra conta dá <c>403</c> ERR-08 e o conflito de
    /// operação dá <c>409</c> com o erro que traz (por exemplo ERR-30 ou ERR-41); a exceção fica tratada.
    /// </summary>
    /// <param name="kind">Exceção do serviço.</param>
    /// <param name="status">Estado HTTP esperado.</param>
    /// <param name="code">Código esperado.</param>
    [Theory]
    [InlineData("not-found", StatusCodes.Status404NotFound, ApiErrorResponse.NotFoundCode)]
    [InlineData("forbidden", StatusCodes.Status403Forbidden, ApiErrorResponse.ElementForbiddenCode)]
    [InlineData("conflict", StatusCodes.Status409Conflict, ApiErrorResponse.InvalidStateCode)]
    public void ServiceExceptionsBecomeCatalogResponses(string kind, int status, string code)
    {
        Exception exception = kind switch
        {
            "not-found" => new ElementNotFoundException(),
            "forbidden" => new ElementForbiddenException(),
            _ => new OperationConflictException(ApiErrorResponse.InvalidState("publicada")),
        };
        ExceptionContext context = NewContext(exception);

        new ServiceExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        ObjectResult result = Assert.IsType<ObjectResult>(context.Result, exactMatch: false);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(code, Assert.IsType<ApiErrorDto>(result.Value).Code);
    }

    /// <summary>
    /// O ficheiro recusado dá <c>400</c> ERR-11 com o motivo no campo <c>file</c>; o pedido acima do limite do servidor
    /// dá <c>413</c> ERR-12.
    /// </summary>
    [Fact]
    public void FileExceptionsBecomeCatalogResponses()
    {
        ExceptionContext rejected = NewContext(new FileRejectedException("O ficheiro excede 5 MB."));
        ExceptionContext tooLarge = NewContext(new BadHttpRequestException("Grande demais.", StatusCodes.Status413PayloadTooLarge));
        var filter = new FileExceptionFilter();

        filter.OnException(rejected);
        filter.OnException(tooLarge);

        ApiErrorDto rejectedBody = Assert.IsType<ApiErrorDto>(Assert.IsType<BadRequestObjectResult>(rejected.Result).Value);
        Assert.Equal(ApiErrorResponse.FileRejectedCode, rejectedBody.Code);
        Assert.Equal(new FieldErrorDto("file", "O ficheiro excede 5 MB."), Assert.Single(rejectedBody.Fields!));
        ObjectResult tooLargeResult = Assert.IsType<ObjectResult>(tooLarge.Result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, tooLargeResult.StatusCode);
        Assert.Equal(ApiErrorResponse.RequestTooLargeCode, Assert.IsType<ApiErrorDto>(tooLargeResult.Value).Code);
        Assert.True(rejected.ExceptionHandled && tooLarge.ExceptionHandled);
    }

    /// <summary>
    /// A validação falhada dá <c>400</c> ERR-10 com todos os campos em erro, pela ordem.
    /// </summary>
    [Fact]
    public void ValidationFailedBecomesErr10WithAllFields()
    {
        FieldErrorDto[] fields = [new("email", FieldReasons.Required), new("phoneNumber", FieldReasons.InvalidValue)];
        ExceptionContext context = NewContext(new ValidationFailedException(fields));

        new ValidationFailedExceptionFilter().OnException(context);

        ApiErrorDto body = Assert.IsType<ApiErrorDto>(Assert.IsType<BadRequestObjectResult>(context.Result).Value);
        Assert.Equal(ApiErrorResponse.ValidationFailedCode, body.Code);
        Assert.Equal(fields, body.Fields);
        Assert.True(context.ExceptionHandled);
    }

    /// <summary>
    /// As exceções que um filtro não conhece seguem para o filtro seguinte (incluindo outro <c>400</c> do servidor que
    /// não seja o <c>413</c>).
    /// </summary>
    [Fact]
    public void UnknownExceptionsAreLeftAlone()
    {
        IExceptionFilter[] filters = [new ServiceExceptionFilter(), new FileExceptionFilter(), new ValidationFailedExceptionFilter()];
        Exception[] exceptions = [new InvalidOperationException("outra"), new BadHttpRequestException("Pedido mal formado.")];

        foreach (IExceptionFilter filter in filters)
        {
            foreach (Exception exception in exceptions)
            {
                ExceptionContext context = NewContext(exception);
                filter.OnException(context);
                Assert.False(context.ExceptionHandled);
                Assert.Null(context.Result);
            }
        }
    }

    /// <summary>
    /// Sem contexto, os filtros recusam o pedido em vez de ignorar o erro.
    /// </summary>
    [Fact]
    public void NullContextIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceExceptionFilter().OnException(null!));
        Assert.Throws<ArgumentNullException>(() => new FileExceptionFilter().OnException(null!));
        Assert.Throws<ArgumentNullException>(() => new ValidationFailedExceptionFilter().OnException(null!));
    }

    private static ExceptionContext NewContext(Exception exception)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, []) { Exception = exception };
    }
}
