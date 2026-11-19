// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Controllers;
using Katch.Backend.Controllers.Mapping;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;
using Katch.Backend.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Testes dos controllers do Administrador — <see cref="AdminAccountsController"/>,
/// <see cref="AdminCompaniesController"/> e <see cref="AdminIndicatorsController"/> (documentação da API v02, secções
/// 6.10 a 6.12) — com os serviços substituídos por duplos: as operações registam o Administrador da credencial, as
/// listas devolvem <c>200</c> com os modelos do serviço no formato da API (I138) e as decisões <c>204</c>; o período dos indicadores é validado no controller, com todos os
/// campos em erro na mesma resposta.
/// </summary>
public sealed class AdminControllersTests
{
    private readonly Guid adminId = Guid.NewGuid();
    private readonly Guid targetId = Guid.NewGuid();

    /// <summary>
    /// As listas de contas e de Candidatos devolvem <c>200</c>; bloquear, suspender e reativar devolvem <c>204</c> com o
    /// Administrador da credencial e a conta da rota (6.10; RF085 a RF090).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AccountsListAndDecisions()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var service = new ServiceDouble<IAccountAdminService>();
        List<AccountModel> accounts = [];
        List<CandidateAdminModel> candidates = [];
        service
            .Returns(nameof(IAccountAdminService.ListAccountsAsync), accounts)
            .Returns(nameof(IAccountAdminService.ListCandidatesAsync), candidates);
        AdminAccountsController controller = new AdminAccountsController(service.Service).WithUser(this.adminId, AccountRoles.Admin);

        Assert.Empty(Assert.IsType<List<AccountDto>>(Assert.IsType<OkObjectResult>((await controller.ListAccounts(token)).Result).Value));
        Assert.Empty(Assert.IsType<List<CandidateAdminDto>>(Assert.IsType<OkObjectResult>((await controller.ListCandidates(token)).Result).Value));
        Assert.IsType<NoContentResult>(await controller.Block(this.targetId, token));
        Assert.IsType<NoContentResult>(await controller.SuspendCandidate(this.targetId, token));
        Assert.IsType<NoContentResult>(await controller.Reactivate(this.targetId, token));

        Assert.All(
            service.Calls.Skip(2),
            call =>
            {
                Assert.Equal(this.adminId, call.Arguments[0]);
                Assert.Equal(this.targetId, call.Arguments[1]);
            });
    }

    /// <summary>
    /// As consultas das Empresas devolvem <c>200</c>; aprovar, recusar, suspender e reativar devolvem <c>204</c>; a
    /// recusa sem corpo chega ao serviço como pedido vazio, que exige o motivo (6.11; RF091 a RF095, RF097, RF098, RF116).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CompaniesQueriesAndDecisions()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var service = new ServiceDouble<ICompanyAdminService>();
        List<PendingCompanyModel> pending = [];
        CompanyRegistrationModel registration = ServiceModels.CompanyRegistration;
        List<CompanyAdminModel> companies = [];
        List<PublishedJobAdminModel> jobs = [];
        service
            .Returns(nameof(ICompanyAdminService.ListPendingAsync), pending)
            .Returns(nameof(ICompanyAdminService.GetSubmittedDataAsync), registration)
            .Returns(nameof(ICompanyAdminService.ListCompaniesAsync), companies)
            .Returns(nameof(ICompanyAdminService.ListPublishedJobsAsync), jobs);
        AdminCompaniesController controller = new AdminCompaniesController(service.Service).WithUser(this.adminId, AccountRoles.Admin);

        Assert.Empty(Assert.IsType<List<PendingCompanyDto>>(Assert.IsType<OkObjectResult>((await controller.ListPending(token)).Result).Value));
        Assert.Equal(
            registration.ToDto(),
            Assert.IsType<OkObjectResult>((await controller.GetSubmittedData(this.targetId, token)).Result).Value);
        Assert.Empty(Assert.IsType<List<CompanyAdminDto>>(Assert.IsType<OkObjectResult>((await controller.ListCompanies(token)).Result).Value));
        Assert.Empty(Assert.IsType<List<PublishedJobAdminDto>>(Assert.IsType<OkObjectResult>((await controller.ListPublishedJobs(token)).Result).Value));
        Assert.IsType<NoContentResult>(await controller.Approve(this.targetId, token));
        Assert.IsType<NoContentResult>(await controller.Reject(this.targetId, null, token));
        Assert.IsType<NoContentResult>(await controller.Suspend(this.targetId, token));
        Assert.IsType<NoContentResult>(await controller.Reactivate(this.targetId, token));

        Assert.Equal(this.targetId, service.Calls[1].Arguments[0]);
        ServiceCall reject = service.Calls.Single(call => call.Method == nameof(ICompanyAdminService.RejectAsync));
        Assert.Equal(this.adminId, reject.Arguments[0]);
        Assert.Equal(this.targetId, reject.Arguments[1]);
        Assert.Equal(RejectCompanyInput.Empty, reject.Arguments[2]);
    }

    /// <summary>
    /// Um período válido, com as datas no formato <c>yyyy-MM-dd</c> e espaços nas extremidades, chega ao serviço como
    /// datas (6.12.1; RF096).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task IndicatorsParseThePeriod()
    {
        var service = new ServiceDouble<IIndicatorService>();
        IndicatorsModel indicators = ServiceModels.Indicators;
        service.Returns(nameof(IIndicatorService.GetAsync), indicators);
        AdminIndicatorsController controller = new AdminIndicatorsController(service.Service).WithUser(this.adminId, AccountRoles.Admin);

        ActionResult<IndicatorsDto> result = await controller.Get(" 2026-10-01 ", "2026-10-31", TestContext.Current.CancellationToken);

        Assert.Equal(indicators.ToDto(), result.Value);
        ServiceCall call = service.SingleCall(nameof(IIndicatorService.GetAsync));
        Assert.Equal(new DateOnly(2026, 10, 1), call.Arguments[0]);
        Assert.Equal(new DateOnly(2026, 10, 31), call.Arguments[1]);
    }

    /// <summary>
    /// Datas em falta ou fora do formato são indicadas juntas (ERR-10) e o serviço não é chamado.
    /// </summary>
    /// <param name="from">Valor de <c>from</c>.</param>
    /// <param name="to">Valor de <c>to</c>.</param>
    /// <param name="fromReason">Motivo esperado para <c>from</c>, ou vazio se for válido.</param>
    /// <param name="toReason">Motivo esperado para <c>to</c>, ou vazio se for válido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null, " ", "required", "required")]
    [InlineData("01-10-2026", "2026-10-31", "format", "")]
    [InlineData("2026-10-01", "2026-02-30", "", "format")]
    [InlineData("2026-10-01", null, "", "required")]
    public async Task IndicatorsRejectAMissingOrMalformedPeriod(string? from, string? to, string fromReason, string toReason)
    {
        var service = new ServiceDouble<IIndicatorService>();
        AdminIndicatorsController controller = new AdminIndicatorsController(service.Service).WithUser(this.adminId, AccountRoles.Admin);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => controller.Get(from, to, TestContext.Current.CancellationToken));

        var expected = new List<FieldError>();
        AddExpected(expected, "from", fromReason);
        AddExpected(expected, "to", toReason);
        Assert.Equal(expected, exception.Fields);
        Assert.Empty(service.Calls);
    }

    /// <summary>
    /// Uma credencial sem o identificador da conta nunca chega aos serviços de decisão.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CredentialWithoutAccountIdNeverReachesTheServices()
    {
        var accounts = new ServiceDouble<IAccountAdminService>();
        var companies = new ServiceDouble<ICompanyAdminService>();
        AdminAccountsController accountsController = new AdminAccountsController(accounts.Service).WithUser(null, AccountRoles.Admin);
        AdminCompaniesController companiesController = new AdminCompaniesController(companies.Service).WithUser(null, AccountRoles.Admin);

        await Assert.ThrowsAsync<InvalidOperationException>(() => accountsController.Block(this.targetId, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => companiesController.Approve(this.targetId, TestContext.Current.CancellationToken));

        Assert.Empty(accounts.Calls);
        Assert.Empty(companies.Calls);
    }

    private static void AddExpected(List<FieldError> expected, string field, string reason)
    {
        if (reason == "required")
        {
            expected.Add(new FieldError(field, FieldReasons.Required));
        }
        else if (reason == "format")
        {
            expected.Add(new FieldError(field, FieldReasons.DateFormat));
        }
    }
}
