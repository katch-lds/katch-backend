// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Security.Claims;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Api;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Net.Http.Headers;

namespace Katch.Backend.Controllers;

/// <summary>
/// Registo, dados de registo e página de apresentação da Empresa (documentação da API v02, secção 6.4; modelo de classes
/// do backend v02, secção 6). A Empresa é sempre a do Recrutador da credencial de sessão: não há identificador na rota
/// (RNF006, DC-05). A alteração dos dados de registo e a página, o logótipo e a galeria exigem também a Empresa aprovada
/// (política <c>ApprovedCompany</c>, ERR-07).
/// </summary>
[ApiController]
[Route("api/recruiter/company")]
[Authorize(Policy = AuthorizationPolicies.Recruiter)]
[Produces("application/json")]
public sealed class CompanyController : ControllerBase
{
    /// <summary>
    /// Rota da consulta do estado do pedido, enviada no cabeçalho <c>Location</c> do registo.
    /// </summary>
    internal const string StatusRoute = "/api/recruiter/company/status";

    // 6 MB (5 MB da fotografia mais a margem para que um ficheiro entre 5 e 6 MB chegue ao validador e receba o ERR-11) e
    // 4 KB para as fronteiras e os cabeçalhos do multipart/form-data (documentação da API v02, secção 2.6).
    private const long MaxUploadRequestBytes = (6L * 1024 * 1024) + (4 * 1024);

    private const string UploadFileField = "file";

    private readonly ICompanyService companyService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyController"/> class.
    /// </summary>
    /// <param name="companyService">Serviço do registo da Empresa.</param>
    public CompanyController(ICompanyService companyService)
    {
        this.companyService = companyService;
    }

    /// <summary>
    /// Regista a Empresa do Recrutador (UC07): com os dados válidos, a Empresa fica pendente de aprovação e a submissão
    /// é registada (RF041 a RF043, RF112). Todos os campos em erro são indicados na mesma resposta (RF042).
    /// </summary>
    /// <param name="request">Dados de registo da Empresa.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>201</c> com o estado do pedido, <c>400</c> (ERR-10) com os campos em erro, ou <c>409</c> (ERR-40) se o
    /// Recrutador já registou uma Empresa.
    /// </returns>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CompanyStatusDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyStatusDto>> Register(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CompanyRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        CompanyStatusDto status = await this.companyService.RegisterAsync(
            this.RecruiterId(),
            request ?? CompanyRegistrationRequest.Empty,
            cancellationToken);
        return this.Created(StatusRoute, status);
    }

    /// <summary>
    /// Corrige os dados do pedido recusado e submete-o novamente (UC07): a Empresa volta a pendente, o motivo da recusa
    /// é limpo e a data de submissão é atualizada (RF045, RF112).
    /// </summary>
    /// <param name="request">Dados de registo corrigidos, completos.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com o estado do pedido, <c>400</c> (ERR-10) com os campos em erro, <c>404</c> (ERR-20) sem Empresa
    /// registada, ou <c>409</c> (ERR-30) se a Empresa não estiver recusada.
    /// </returns>
    [HttpPost("resubmission")]
    [Consumes("application/json")]
    [ProducesResponseType<CompanyStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyStatusDto>> Resubmit(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CompanyRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        return await this.companyService.ResubmitAsync(
            this.RecruiterId(),
            request ?? CompanyRegistrationRequest.Empty,
            cancellationToken);
    }

    /// <summary>
    /// Consulta o estado do pedido de registo, com o motivo da recusa no estado recusado (RF044). É o que a área de
    /// gestão web consulta enquanto as operações reservadas estão vedadas (RF039).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o estado do pedido, ou <c>404</c> (ERR-20) se o Recrutador ainda não registou a Empresa.</returns>
    [HttpGet("status")]
    [ProducesResponseType<CompanyStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return await this.companyService.GetStatusAsync(this.RecruiterId(), cancellationToken);
    }

    /// <summary>
    /// Consulta os dados de registo da própria Empresa, em qualquer estado, para pré-preencher os formulários de
    /// correção e de alteração (6.4.8; RF044, RF045, RF049).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com os dados de registo, ou <c>404</c> (ERR-20) se o Recrutador ainda não registou a Empresa.</returns>
    [HttpGet]
    [ProducesResponseType<CompanyRegistrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyRegistrationDto>> GetRegistration(CancellationToken cancellationToken)
    {
        return await this.companyService.GetRegistrationAsync(this.RecruiterId(), cancellationToken);
    }

    /// <summary>
    /// Altera os dados de registo da Empresa aprovada, que se mantém aprovada (6.4.4; RF049, RF112). O NIF tem de ser
    /// igual ao registado.
    /// </summary>
    /// <param name="request">Dados de registo completos, com o mesmo NIF.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com o estado do pedido, <c>400</c> (ERR-10) com os campos em erro (incluindo o NIF diferente do
    /// registado), ou <c>403</c> (ERR-07) se a Empresa não estiver aprovada.
    /// </returns>
    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.ApprovedCompany)]
    [Consumes("application/json")]
    [ProducesResponseType<CompanyStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CompanyStatusDto>> UpdateRegistration(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CompanyRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        return await this.companyService.UpdateRegistrationAsync(
            this.RecruiterId(),
            request ?? CompanyRegistrationRequest.Empty,
            cancellationToken);
    }

    /// <summary>
    /// Consulta a página de apresentação da própria Empresa, para pré-preencher o formulário da página (6.4.9; RF046 a
    /// RF048).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com a página, ou <c>403</c> (ERR-07) se a Empresa não estiver aprovada.</returns>
    [HttpGet("page")]
    [Authorize(Policy = AuthorizationPolicies.ApprovedCompany)]
    [ProducesResponseType<CompanyPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CompanyPageDto>> GetPage(CancellationToken cancellationToken)
    {
        return await this.companyService.GetPageAsync(this.RecruiterId(), cancellationToken);
    }

    /// <summary>
    /// Grava a descrição da atividade e o sítio na Internet da página de apresentação (6.4.5; RF046; P08, P10). Um valor
    /// <see langword="null"/> limpa o campo.
    /// </summary>
    /// <param name="request">Descrição e sítio na Internet.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com a página atualizada, <c>400</c> (ERR-10) com os campos em erro, ou <c>403</c> (ERR-07) se a Empresa
    /// não estiver aprovada.
    /// </returns>
    [HttpPut("page")]
    [Authorize(Policy = AuthorizationPolicies.ApprovedCompany)]
    [Consumes("application/json")]
    [ProducesResponseType<CompanyPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CompanyPageDto>> UpdatePage(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CompanyPageRequest? request,
        CancellationToken cancellationToken)
    {
        return await this.companyService.UpdatePageAsync(
            this.RecruiterId(),
            request ?? CompanyPageRequest.Empty,
            cancellationToken);
    }

    /// <summary>
    /// Guarda o logótipo, em JPEG ou PNG até 2 MB, substituindo o anterior (6.4.6; RF047; RNF016; P01). O pedido é
    /// <c>multipart/form-data</c> com o campo <c>file</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>204</c> com o URL do logótipo no cabeçalho <c>Location</c>, <c>400</c> (ERR-11), <c>403</c> (ERR-07),
    /// <c>413</c> (ERR-12) ou <c>415</c> (ERR-13).
    /// </returns>
    [HttpPut("logo")]
    [Authorize(Policy = AuthorizationPolicies.ApprovedCompany)]
    [UploadRequestFilter(MaxUploadRequestBytes)]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> UploadLogo(CancellationToken cancellationToken)
    {
        IFormFile? file = await this.ReadUploadAsync(cancellationToken);
        string url = await this.companyService.UploadLogoAsync(this.RecruiterId(), file, cancellationToken);
        this.Response.Headers[HeaderNames.Location] = url;
        return this.NoContent();
    }

    /// <summary>
    /// Acrescenta uma fotografia, em JPEG ou PNG até 5 MB, no fim da galeria (6.4.7; RF048; RNF016; P01, P13). O pedido
    /// é <c>multipart/form-data</c> com o campo <c>file</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>201</c> com o URL da fotografia no cabeçalho <c>Location</c>, <c>400</c> (ERR-11), <c>403</c> (ERR-07),
    /// <c>409</c> (ERR-39) se a galeria já tiver 6 fotografias, <c>413</c> (ERR-12) ou <c>415</c> (ERR-13).
    /// </returns>
    [HttpPost("photos")]
    [Authorize(Policy = AuthorizationPolicies.ApprovedCompany)]
    [UploadRequestFilter(MaxUploadRequestBytes)]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> AddGalleryPhoto(CancellationToken cancellationToken)
    {
        IFormFile? file = await this.ReadUploadAsync(cancellationToken);
        string url = await this.companyService.AddGalleryPhotoAsync(this.RecruiterId(), file, cancellationToken);
        return this.Created(url, null);
    }

    private async Task<IFormFile?> ReadUploadAsync(CancellationToken cancellationToken)
    {
        IFormCollection form = await this.Request.ReadFormAsync(cancellationToken);
        return form.Files.GetFile(UploadFileField);
    }

    /// <summary>
    /// Identificador do Recrutador, lido do <c>sub</c> da credencial de sessão (nunca da rota nem do corpo).
    /// </summary>
    private Guid RecruiterId()
    {
        return Guid.TryParse(this.User.FindFirstValue("sub"), out Guid recruiterId)
            ? recruiterId
            : throw new InvalidOperationException("A credencial de sessão não identifica a conta.");
    }
}
