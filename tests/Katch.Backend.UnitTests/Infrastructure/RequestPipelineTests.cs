// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Api;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Katch.Backend.UnitTests.Infrastructure;

/// <summary>
/// Testes das peças da cadeia do pedido que não dependem da base de dados: <see cref="ApiErrorAuthorizationResultHandler"/>
/// (ERR-01, ERR-06, ERR-07), <see cref="UploadRequestFilterAttribute"/> (ERR-12, ERR-13) e
/// <see cref="OperationLogger"/> (registo com o relógio do servidor). O <c>AccountStatusMiddleware</c> e o
/// <c>ApprovedCompanyAuthorizationHandler</c> consultam o <c>KatchDbContext</c> e são verificados nos testes de
/// integração.
/// </summary>
public sealed class RequestPipelineTests
{
    private static readonly AuthorizationPolicy Policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    /// <summary>
    /// Sem sessão válida a resposta é <c>401</c> ERR-01 e o pedido não continua.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ChallengedRequestIsErr01()
    {
        var next = new RecordingNext();
        DefaultHttpContext context = NewHttpContext();

        await new ApiErrorAuthorizationResultHandler().HandleAsync(next.Invoke, context, Policy, PolicyAuthorizationResult.Challenge());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal(ApiErrorResponse.InvalidSessionCode, await ReadCodeAsync(context));
        Assert.False(next.Called);
    }

    /// <summary>
    /// A recusa vinda do handler da Empresa aprovada dá <c>403</c> ERR-07 com a mensagem do handler; qualquer outra
    /// recusa dá <c>403</c> ERR-06 (tipo de conta não autorizado).
    /// </summary>
    /// <param name="fromCompanyHandler">Se a recusa vem do handler da Empresa aprovada.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForbiddenRequestIsErr07OrErr06(bool fromCompanyHandler)
    {
        var next = new RecordingNext();
        DefaultHttpContext context = NewHttpContext();
        string message = ApiErrorResponse.CompanyNotApprovedMessage(CompanyStatus.Rejected);
        AuthorizationFailure failure = fromCompanyHandler
            ? AuthorizationFailure.Failed([new AuthorizationFailureReason(new ApprovedCompanyAuthorizationHandler(null!), message)])
            : AuthorizationFailure.Failed([new RolesAuthorizationRequirement([AccountRoles.Admin])]);

        await new ApiErrorAuthorizationResultHandler().HandleAsync(next.Invoke, context, Policy, PolicyAuthorizationResult.Forbid(failure));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal(
            fromCompanyHandler ? ApiErrorResponse.CompanyNotApprovedCode : ApiErrorResponse.AccountTypeNotAllowedCode,
            await ReadCodeAsync(context));
        Assert.False(next.Called);
    }

    /// <summary>
    /// Com a autorização concedida, o pedido continua sem resposta de erro.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AuthorizedRequestContinues()
    {
        var next = new RecordingNext();
        DefaultHttpContext context = NewHttpContext();

        await new ApiErrorAuthorizationResultHandler().HandleAsync(next.Invoke, context, Policy, PolicyAuthorizationResult.Success());

        Assert.True(next.Called);
        Assert.Equal(0, context.Response.Body.Length);
    }

    /// <summary>
    /// O filtro de carregamento aceita só <c>multipart/form-data</c> (ERR-13, <c>415</c>) e recusa o pedido acima do
    /// limite antes de ler o corpo (ERR-12, <c>413</c>); no limite, o pedido segue.
    /// </summary>
    /// <param name="contentType">Tipo de conteúdo do pedido.</param>
    /// <param name="length">Dimensão declarada do pedido.</param>
    /// <param name="expectedStatus">Estado esperado, ou 0 se o pedido seguir.</param>
    [Theory]
    [InlineData("multipart/form-data; boundary=x", 1000L, 0)]
    [InlineData("MULTIPART/FORM-DATA; boundary=x", 1000L, 0)]
    [InlineData("multipart/form-data; boundary=x", 1001L, StatusCodes.Status413PayloadTooLarge)]
    [InlineData("application/json", 10L, StatusCodes.Status415UnsupportedMediaType)]
    [InlineData(null, 10L, StatusCodes.Status415UnsupportedMediaType)]
    [InlineData("não é um tipo", 10L, StatusCodes.Status415UnsupportedMediaType)]
    public void UploadFilterChecksTypeAndSize(string? contentType, long length, int expectedStatus)
    {
        DefaultHttpContext http = NewHttpContext();
        http.Request.ContentType = contentType;
        http.Request.ContentLength = length;
        var context = new ResourceExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            [],
            []);
        var filter = new UploadRequestFilterAttribute(1000);

        filter.OnResourceExecuting(context);
        filter.OnResourceExecuted(null!);

        if (expectedStatus == 0)
        {
            Assert.Null(context.Result);
            return;
        }

        ObjectResult result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(expectedStatus, result.StatusCode);
    }

    /// <summary>
    /// O registo de operação usa o instante do relógio do servidor, a conta e a entidade indicadas (RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OperationLoggerRecordsWithTheServerClock()
    {
        var now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        var repository = new RecordingOperationLogRepository();
        var logger = new OperationLogger(repository, new FixedClock(now));
        Guid userId = Guid.NewGuid();
        Guid entityId = Guid.NewGuid();

        await logger.LogAsync(userId, OperationType.JobClosed, entityId, TestContext.Current.CancellationToken);

        OperationLog entry = Assert.Single(repository.Added);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(OperationType.JobClosed, entry.Operation);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(now, entry.OccurredAt);
    }

    private static DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string?> ReadCodeAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: context.RequestAborted);
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed class RecordingNext
    {
        public bool Called { get; private set; }

        public RequestDelegate Invoke => _ =>
        {
            this.Called = true;
            return Task.CompletedTask;
        };
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class RecordingOperationLogRepository : IOperationLogRepository
    {
        public List<OperationLog> Added { get; } = [];

        public Task AddAsync(OperationLog entry, CancellationToken cancellationToken = default)
        {
            this.Added.Add(entry);
            return Task.CompletedTask;
        }
    }
}
