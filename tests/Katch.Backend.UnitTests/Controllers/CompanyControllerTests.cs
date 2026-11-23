// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Controllers;
using Katch.Backend.Controllers.Mapping;
using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Services;
using Katch.Backend.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Testes do <see cref="CompanyController"/> (documentação da API v02, secção 6.4) com o serviço substituído por um
/// duplo: a Empresa é sempre a do Recrutador da credencial (RNF006), o registo devolve <c>201</c> com a rota do estado
/// e os carregamentos devolvem o URL do ficheiro. As regras do registo são verificadas nos testes do
/// <see cref="CompanyService"/>.
/// </summary>
public sealed class CompanyControllerTests
{
    private readonly Guid recruiterId = Guid.NewGuid();
    private readonly ServiceDouble<ICompanyService> service = new();
    private readonly FileUrlBuilder fileUrlBuilder = new();
    private readonly CompanyController controller;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyControllerTests"/> class.
    /// </summary>
    public CompanyControllerTests()
    {
        this.controller = new CompanyController(this.service.Service, this.fileUrlBuilder).WithUser(this.recruiterId, AccountRoles.Recruiter);
    }

    /// <summary>
    /// O registo devolve <c>201</c> com o estado do pedido e a rota da consulta do estado em <c>Location</c>; sem corpo,
    /// o serviço recebe o pedido vazio e indica os campos obrigatórios (RF041, RF042).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RegisterReturnsCreatedWithTheStatusRoute()
    {
        CompanyStatusModel status = ServiceModels.RegistrationStatus;
        this.service.Returns(nameof(ICompanyService.RegisterAsync), status);

        ActionResult<CompanyStatusDto> result = await this.controller.Register(null, TestContext.Current.CancellationToken);

        CreatedResult created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/recruiter/company/status", created.Location);
        Assert.Equal(status.ToDto(), created.Value);
        ServiceCall call = this.service.SingleCall(nameof(ICompanyService.RegisterAsync));
        Assert.Equal(this.recruiterId, call.Arguments[0]);
        Assert.Equal(CompanyRegistrationInput.Empty, call.Arguments[1]);
    }

    /// <summary>
    /// A nova submissão, a consulta do estado e dos dados, a alteração e a página devolvem o resultado do serviço para o
    /// Recrutador da credencial, no formato da API; a página é construída a partir da Empresa devolvida pelo serviço,
    /// com os URL da API do logótipo e da galeria (RF044, RF045, RF046, RF049, I138).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReadsAndUpdatesReturnTheServiceResult()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        CompanyStatusModel status = ServiceModels.RegistrationStatus;
        CompanyRegistrationModel registration = ServiceModels.CompanyRegistration;
        Company page = ServiceModels.Company;
        this.service
            .Returns(nameof(ICompanyService.ResubmitAsync), status)
            .Returns(nameof(ICompanyService.GetStatusAsync), status)
            .Returns(nameof(ICompanyService.GetRegistrationAsync), registration)
            .Returns(nameof(ICompanyService.UpdateRegistrationAsync), status)
            .Returns(nameof(ICompanyService.GetPageAsync), page)
            .Returns(nameof(ICompanyService.UpdatePageAsync), page);

        CompanyPageDto expectedPage = page.ToPageDto(this.fileUrlBuilder);

        Assert.Equal(status.ToDto(), (await this.controller.Resubmit(null, token)).Value);
        Assert.Equal(status.ToDto(), (await this.controller.GetStatus(token)).Value);
        Assert.Equal(registration.ToDto(), (await this.controller.GetRegistration(token)).Value);
        Assert.Equal(status.ToDto(), (await this.controller.UpdateRegistration(null, token)).Value);
        Assert.Equivalent(expectedPage, (await this.controller.GetPage(token)).Value, strict: true);
        Assert.Equivalent(expectedPage, (await this.controller.UpdatePage(null, token)).Value, strict: true);
        Assert.Equal("/api/files/COMPANY_LOGO/0123456789abcdef0123456789abcdef.png", expectedPage.LogoUrl);

        Assert.All(this.service.Calls, call => Assert.Equal(this.recruiterId, call.Arguments[0]));
        Assert.Equal(CompanyRegistrationInput.Empty, this.service.Calls[0].Arguments[1]);
        Assert.Equal(CompanyRegistrationInput.Empty, this.service.Calls[3].Arguments[1]);
        Assert.Equal(CompanyPageInput.Empty, this.service.Calls[5].Arguments[1]);
    }

    /// <summary>
    /// O logótipo guardado dá <c>204</c> com o URL em <c>Location</c> (6.4.6) e a fotografia da galeria dá <c>201</c>
    /// com o URL (6.4.7); o ficheiro do campo <c>file</c> chega ao serviço.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UploadsReturnTheFileUrl()
    {
        IFormFile? file = this.controller.WithUpload();
        this.service
            .Returns(nameof(ICompanyService.UploadLogoAsync), "/api/files/COMPANY_LOGO/l.png")
            .Returns(nameof(ICompanyService.AddGalleryPhotoAsync), "/api/files/COMPANY_PHOTO/g.png");

        IActionResult logo = await this.controller.UploadLogo(TestContext.Current.CancellationToken);
        IActionResult photo = await this.controller.AddGalleryPhoto(TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(logo);
        Assert.Equal("/api/files/COMPANY_LOGO/l.png", this.controller.Response.Headers[HeaderNames.Location]);
        CreatedResult created = Assert.IsType<CreatedResult>(photo);
        Assert.Equal("/api/files/COMPANY_PHOTO/g.png", created.Location);
        Assert.All(this.service.Calls, call => Assert.Same(file, call.Arguments[1]));
    }

    /// <summary>
    /// Uma credencial sem um <c>sub</c> válido nunca chega ao serviço.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CredentialWithoutAccountIdNeverReachesTheService()
    {
        this.controller.WithUser(null, AccountRoles.Recruiter);

        await Assert.ThrowsAsync<InvalidOperationException>(() => this.controller.GetStatus(TestContext.Current.CancellationToken));

        Assert.Empty(this.service.Calls);
    }
}
