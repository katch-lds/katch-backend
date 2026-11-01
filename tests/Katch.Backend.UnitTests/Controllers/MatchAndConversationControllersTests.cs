// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Controllers;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Testes dos controllers do processo de match — <see cref="CandidateEvaluationController"/> (decisão do Recrutador),
/// <see cref="MatchesController"/> e <see cref="ConversationsController"/> (documentação da API v02, secções 6.6 a 6.8)
/// — com os serviços substituídos por duplos. As transições do match (espera, aceitação, recusa) são verificadas nos
/// testes da entidade match e dos serviços; aqui verifica-se o encaminhamento, os códigos de estado e as regras
/// próprias dos controllers (cabeçalho <c>no-store</c> do curriculum vitae, escolha da lista por tipo de conta, Empresa
/// aprovada para o Recrutador, leitura do parâmetro <c>after</c>).
/// </summary>
public sealed class MatchAndConversationControllersTests
{
    private readonly Guid accountId = Guid.NewGuid();
    private readonly Guid matchId = Guid.NewGuid();

    /// <summary>
    /// A lista de espera, o perfil e a aceitação devolvem o resultado do serviço; a recusa devolve <c>204</c>; todas usam
    /// o Recrutador da credencial e o interesse da rota (6.6; RF060 a RF066).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EvaluationOperations()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var service = new ServiceDouble<ICandidateEvaluationService>();
        List<WaitingCandidateDto> waiting = [];
        var profile = ControllerTesting.Sentinel<CandidateFullProfileDto>();
        var match = ControllerTesting.Sentinel<MatchDto>();
        service
            .Returns(nameof(ICandidateEvaluationService.ListWaitingAsync), waiting)
            .Returns(nameof(ICandidateEvaluationService.OpenProfileAsync), profile)
            .Returns(nameof(ICandidateEvaluationService.AcceptAsync), match);
        CandidateEvaluationController controller = new CandidateEvaluationController(service.Service)
            .WithUser(this.accountId, AccountRoles.Recruiter);
        Guid jobId = Guid.NewGuid();

        Assert.Same(waiting, (await controller.ListWaiting(jobId, token)).Value);
        Assert.Same(profile, (await controller.OpenProfile(this.matchId, token)).Value);
        Assert.Same(match, (await controller.Accept(this.matchId, token)).Value);
        Assert.IsType<NoContentResult>(await controller.Reject(this.matchId, token));

        Assert.All(service.Calls, call => Assert.Equal(this.accountId, call.Arguments[0]));
        Assert.Equal(jobId, service.Calls[0].Arguments[1]);
        Assert.All(service.Calls.Skip(1), call => Assert.Equal(this.matchId, call.Arguments[1]));
    }

    /// <summary>
    /// O curriculum vitae do Candidato é enviado com o nome do ficheiro e sem guardar em cache (<c>no-store</c>, RNF007).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EvaluationCvIsNotCached()
    {
        var service = new ServiceDouble<ICandidateEvaluationService>();
        using var content = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        service.Returns(nameof(ICandidateEvaluationService.GetCvAsync), new FileContent(content, "application/pdf", "cv.pdf"));
        CandidateEvaluationController controller = new CandidateEvaluationController(service.Service)
            .WithUser(this.accountId, AccountRoles.Recruiter);

        IActionResult result = await controller.GetCv(this.matchId, TestContext.Current.CancellationToken);

        FileStreamResult file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("cv.pdf", file.FileDownloadName);
        Assert.Equal("no-store", controller.Response.Headers[HeaderNames.CacheControl]);
    }

    /// <summary>
    /// O Candidato recebe a sua lista de matches sem avaliar a política da Empresa aprovada (6.7.1; RF024).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CandidateMatchesSkipTheCompanyPolicy()
    {
        var service = new ServiceDouble<IMatchService>();
        var authorization = new ServiceDouble<IAuthorizationService>();
        List<MatchDto> matches = [];
        service.Returns(nameof(IMatchService.ListForCandidateAsync), matches);
        MatchesController controller = new MatchesController(service.Service, authorization.Service)
            .WithUser(this.accountId, AccountRoles.Candidate);

        ActionResult<List<MatchDto>> result = await controller.List(TestContext.Current.CancellationToken);

        Assert.Same(matches, result.Value);
        Assert.Equal(this.accountId, service.SingleCall(nameof(IMatchService.ListForCandidateAsync)).Arguments[0]);
        Assert.Empty(authorization.Calls);
    }

    /// <summary>
    /// O Recrutador com a Empresa aprovada recebe a lista da Empresa (6.7.1; RF067).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RecruiterMatchesRequireTheApprovedCompany()
    {
        var service = new ServiceDouble<IMatchService>();
        var authorization = new ServiceDouble<IAuthorizationService>();
        List<MatchDto> matches = [];
        service.Returns(nameof(IMatchService.ListForRecruiterAsync), matches);
        authorization.Returns(nameof(IAuthorizationService.AuthorizeAsync), AuthorizationResult.Success());
        MatchesController controller = new MatchesController(service.Service, authorization.Service)
            .WithUser(this.accountId, AccountRoles.Recruiter);

        ActionResult<List<MatchDto>> result = await controller.List(TestContext.Current.CancellationToken);

        Assert.Same(matches, result.Value);
        Assert.Equal(AuthorizationPolicies.ApprovedCompany, authorization.SingleCall(nameof(IAuthorizationService.AuthorizeAsync)).Arguments[2]);
    }

    /// <summary>
    /// Sem a Empresa aprovada o Recrutador recebe <c>403</c> com o ERR-07 e a mensagem do handler da política (com o
    /// estado da Empresa), ou a mensagem genérica se a falha não vier desse handler; a lista não é consultada.
    /// </summary>
    /// <param name="fromCompanyHandler">Se a falha traz o motivo do handler da Empresa aprovada.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RecruiterWithoutApprovedCompanyIsForbidden(bool fromCompanyHandler)
    {
        var service = new ServiceDouble<IMatchService>();
        var authorization = new ServiceDouble<IAuthorizationService>();
        string pendingMessage = ApiErrorResponse.CompanyNotApprovedMessage(CompanyStatus.Pending);
        AuthorizationResult failed = fromCompanyHandler
            ? AuthorizationResult.Failed(AuthorizationFailure.Failed([new AuthorizationFailureReason(new ApprovedCompanyAuthorizationHandler(null!), pendingMessage)]))
            : AuthorizationResult.Failed();
        authorization.Returns(nameof(IAuthorizationService.AuthorizeAsync), failed);
        MatchesController controller = new MatchesController(service.Service, authorization.Service)
            .WithUser(this.accountId, AccountRoles.Recruiter);

        ActionResult<List<MatchDto>> result = await controller.List(TestContext.Current.CancellationToken);

        ObjectResult forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        ApiErrorDto body = Assert.IsType<ApiErrorDto>(forbidden.Value);
        Assert.Equal(ApiErrorResponse.CompanyNotApprovedCode, body.Code);
        Assert.Equal(fromCompanyHandler ? pendingMessage : ApiErrorResponse.CompanyNotApprovedMessage(null), body.Message);
        Assert.Empty(service.Calls);
    }

    /// <summary>
    /// A lista e a abertura das conversas devolvem o resultado do serviço; o envio devolve <c>201</c> sem
    /// <c>Location</c> e o encerramento <c>204</c>; o envio sem corpo chega como pedido vazio (6.8; RF026 a RF030, RF069 a RF074).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConversationOperations()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var service = new ServiceDouble<IConversationService>();
        List<ConversationSummaryDto> conversations = [];
        List<MessageDto> messages = [];
        var message = ControllerTesting.Sentinel<MessageDto>();
        service
            .Returns(nameof(IConversationService.ListAsync), conversations)
            .Returns(nameof(IConversationService.OpenAsync), messages)
            .Returns(nameof(IConversationService.SendAsync), message);
        ConversationsController controller = new ConversationsController(service.Service)
            .WithUser(this.accountId, AccountRoles.Candidate);

        Assert.Same(conversations, (await controller.List(token)).Value);
        Assert.Same(messages, (await controller.Open(this.matchId, null, token)).Value);
        ObjectResult sent = Assert.IsType<ObjectResult>((await controller.Send(this.matchId, null, token)).Result);
        Assert.IsType<NoContentResult>(await controller.Close(this.matchId, token));

        Assert.Equal(StatusCodes.Status201Created, sent.StatusCode);
        Assert.Same(message, sent.Value);
        Assert.Null(service.Calls[1].Arguments[2]);
        Assert.Same(SendMessageRequest.Empty, service.Calls[2].Arguments[2]);
        Assert.All(service.Calls, call => Assert.Equal(this.accountId, call.Arguments[0]));
    }

    /// <summary>
    /// O parâmetro <c>after</c> é lido como instante em UTC (sem fuso, assume-se UTC); um valor que não é uma data dá
    /// o ERR-10 com o campo <c>after</c> e o serviço não é chamado (6.8.2).
    /// </summary>
    /// <param name="after">Valor do parâmetro.</param>
    /// <param name="expectedUtc">Instante esperado em UTC, ou vazio se o valor for inválido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("2026-10-31T10:00:00Z", "2026-10-31T10:00:00+00:00")]
    [InlineData("2026-10-31T11:00:00+01:00", "2026-10-31T10:00:00+00:00")]
    [InlineData("2026-10-31T10:00:00", "2026-10-31T10:00:00+00:00")]
    [InlineData("ontem", "")]
    public async Task ConversationAfterIsReadInUtc(string after, string expectedUtc)
    {
        var service = new ServiceDouble<IConversationService>();
        service.Returns(nameof(IConversationService.OpenAsync), new List<MessageDto>());
        ConversationsController controller = new ConversationsController(service.Service)
            .WithUser(this.accountId, AccountRoles.Recruiter);

        Exception? exception = await Record.ExceptionAsync(() => controller.Open(this.matchId, after, TestContext.Current.CancellationToken));

        if (expectedUtc.Length == 0)
        {
            ValidationFailedException invalid = Assert.IsType<ValidationFailedException>(exception);
            Assert.Equal(new FieldErrorDto("after", FieldReasons.InvalidValue), Assert.Single(invalid.Fields));
            Assert.Empty(service.Calls);
            return;
        }

        Assert.Null(exception);
        DateTimeOffset parsed = Assert.IsType<DateTimeOffset>(service.SingleCall(nameof(IConversationService.OpenAsync)).Arguments[2]);
        Assert.Equal(DateTimeOffset.Parse(expectedUtc, CultureInfo.InvariantCulture), parsed);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
    }

    /// <summary>
    /// Uma credencial sem o identificador da conta nunca chega aos serviços.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CredentialWithoutAccountIdNeverReachesTheServices()
    {
        var evaluation = new ServiceDouble<ICandidateEvaluationService>();
        var matches = new ServiceDouble<IMatchService>();
        var conversations = new ServiceDouble<IConversationService>();
        var authorization = new ServiceDouble<IAuthorizationService>();
        CancellationToken token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new CandidateEvaluationController(evaluation.Service).WithUser(null, AccountRoles.Recruiter).Accept(this.matchId, token));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new MatchesController(matches.Service, authorization.Service).WithUser(null, AccountRoles.Candidate).List(token));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ConversationsController(conversations.Service).WithUser(null, AccountRoles.Candidate).List(token));

        Assert.Empty(evaluation.Calls);
        Assert.Empty(matches.Calls);
        Assert.Empty(conversations.Calls);
    }
}
