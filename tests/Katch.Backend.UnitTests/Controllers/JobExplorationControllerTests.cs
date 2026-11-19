// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Controllers;
using Katch.Backend.Controllers.Mapping;
using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Services;
using Katch.Backend.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Testes do <see cref="JobExplorationController"/> (documentação da API v02, secção 6.3) com o serviço substituído
/// por um duplo: sem vagas o cartão seguinte é <c>204</c>, o interesse devolve a quota e a quota esgotada (cooldown,
/// ERR-31) sobe para o filtro de exceções. As regras da quota e do cooldown são verificadas nos testes da entidade
/// Candidato e de <see cref="JobExplorationService"/>.
/// </summary>
public sealed class JobExplorationControllerTests
{
    private readonly Guid candidateId = Guid.NewGuid();
    private readonly Guid jobId = Guid.NewGuid();
    private readonly ServiceDouble<IJobExplorationService> service = new();
    private readonly FileUrlBuilder fileUrlBuilder = new();
    private readonly JobExplorationController controller;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExplorationControllerTests"/> class.
    /// </summary>
    public JobExplorationControllerTests()
    {
        this.controller = new JobExplorationController(this.service.Service, this.fileUrlBuilder).WithUser(this.candidateId, AccountRoles.Candidate);
    }

    /// <summary>
    /// Sem vagas disponíveis o cartão seguinte é <c>204</c>; com vaga, é <c>200</c> com o cartão (6.3.1).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NextCardIsNoContentWhenThereAreNoJobs()
    {
        ActionResult<JobCardDto> none = await this.controller.GetNextCard(TestContext.Current.CancellationToken);
        JobCardModel card = ServiceModels.JobCard;
        this.service.Returns(nameof(IJobExplorationService.GetNextCardAsync), card);
        ActionResult<JobCardDto> next = await this.controller.GetNextCard(TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(none.Result);
        Assert.Equivalent(card.ToDto(), Assert.IsType<OkObjectResult>(next.Result).Value, strict: true);
        Assert.All(this.service.Calls, call => Assert.Equal(this.candidateId, call.Arguments[0]));
    }

    /// <summary>
    /// O detalhe, a quota e a página da Empresa devolvem <c>200</c> com o resultado do serviço no formato da API; a página
    /// é construída a partir da Empresa devolvida pelo serviço (6.3.2, 6.3.6, 6.3.3, I138).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReadsReturnTheServiceResult()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        JobDetailModel detail = ServiceModels.JobDetail;
        var quota = new QuotaModel(7, Candidate.MaxInterests, null);
        Company page = ServiceModels.Company;
        Guid companyId = Guid.NewGuid();
        this.service
            .Returns(nameof(IJobExplorationService.GetDetailAsync), detail)
            .Returns(nameof(IJobExplorationService.GetQuotaAsync), quota)
            .Returns(nameof(IJobExplorationService.GetCompanyPageAsync), page);

        Assert.Equivalent(
            detail.ToDto(),
            Assert.IsType<OkObjectResult>((await this.controller.GetDetail(this.jobId, token)).Result).Value,
            strict: true);
        Assert.Equal(quota.ToDto(), Assert.IsType<OkObjectResult>((await this.controller.GetQuota(token)).Result).Value);
        Assert.Equivalent(
            page.ToPageDto(this.fileUrlBuilder),
            (await this.controller.GetCompanyPage(companyId, token)).Value,
            strict: true);

        Assert.Equal(this.jobId, this.service.Calls[0].Arguments[1]);
        Assert.Equal(companyId, this.service.Calls[2].Arguments[0]);
    }

    /// <summary>
    /// A recusa devolve <c>204</c> e o interesse devolve <c>200</c> com a quota restante (6.3.4, 6.3.5; RF018, RF019).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DeclineAndInterest()
    {
        var quota = new QuotaModel(9, Candidate.MaxInterests, null);
        this.service.Returns(nameof(IJobExplorationService.ExpressInterestAsync), quota);

        IActionResult declined = await this.controller.Decline(this.jobId, TestContext.Current.CancellationToken);
        ActionResult<QuotaDto> interest = await this.controller.ExpressInterest(this.jobId, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(declined);
        Assert.Equal(quota.ToDto(), Assert.IsType<OkObjectResult>(interest.Result).Value);
        Assert.All(this.service.Calls, call => Assert.Equal(this.jobId, call.Arguments[1]));
    }

    /// <summary>
    /// Com a quota esgotada (cooldown), a exceção do serviço sobe sem ser convertida no controller.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ExhaustedQuotaPropagates()
    {
        var exhausted = new QuotaExhaustedException(DateTimeOffset.UtcNow.AddHours(1), TimeSpan.FromHours(1));
        this.service.Throws(nameof(IJobExplorationService.ExpressInterestAsync), exhausted);

        QuotaExhaustedException exception = await Assert.ThrowsAsync<QuotaExhaustedException>(
            () => this.controller.ExpressInterest(this.jobId, TestContext.Current.CancellationToken));

        Assert.Same(exhausted, exception);
    }

    /// <summary>
    /// Uma credencial sem o identificador da conta nunca chega ao serviço.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CredentialWithoutAccountIdNeverReachesTheService()
    {
        this.controller.WithUser(null, AccountRoles.Candidate);

        await Assert.ThrowsAsync<InvalidOperationException>(() => this.controller.GetQuota(TestContext.Current.CancellationToken));

        Assert.Empty(this.service.Calls);
    }
}
