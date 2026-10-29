// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

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
/// Perfil profissional do Candidato (documentação da API v02, secções 6.2.1 a 6.2.3 e 6.2.6 a 6.2.9; modelo de classes
/// do backend v02, secção 6). O Candidato é sempre o da credencial de sessão (claim <c>sub</c>), nunca o do pedido.
/// </summary>
[ApiController]
[Route("api/candidate/profile")]
[Authorize(Policy = AuthorizationPolicies.Candidate)]
[Produces("application/json")]
public sealed class CandidateProfileController : ControllerBase
{
    // 6 MB (5 MB do ficheiro mais a margem para que um ficheiro entre 5 e 6 MB chegue ao validador e receba o ERR-11) e
    // 4 KB para as fronteiras e os cabeçalhos do multipart/form-data.
    private const long MaxUploadRequestBytes = (6L * 1024 * 1024) + (4 * 1024);

    private const string UploadFileField = "file";

    private readonly ICandidateProfileService profileService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateProfileController"/> class.
    /// </summary>
    /// <param name="profileService">Serviço do perfil do Candidato.</param>
    public CandidateProfileController(ICandidateProfileService profileService)
    {
        this.profileService = profileService;
    }

    /// <summary>
    /// Consulta o perfil profissional do Candidato (6.2.1; RF006 a RF012).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o perfil.</returns>
    [HttpGet]
    [ProducesResponseType<CandidateProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CandidateProfileDto>> Get(CancellationToken cancellationToken)
    {
        return await this.profileService.GetAsync(this.GetCandidateId(), cancellationToken);
    }

    /// <summary>
    /// Grava a função pretendida, a localidade, a disponibilidade e o resumo da experiência (6.2.2; RF006; P08, P14).
    /// </summary>
    /// <param name="request">Novos valores dos quatro campos; <see langword="null"/> limpa os opcionais.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o perfil atualizado, ou <c>400</c> (ERR-10) com todos os campos em erro.</returns>
    [HttpPut]
    [ProducesResponseType<CandidateProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CandidateProfileDto>> Update(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UpdateCandidateProfileRequest? request,
        CancellationToken cancellationToken)
    {
        return await this.profileService.UpdateAsync(
            this.GetCandidateId(),
            request ?? UpdateCandidateProfileRequest.Empty,
            cancellationToken);
    }

    /// <summary>
    /// Grava as preferências de procura, aplicadas ao cartão seguinte (6.2.3; RF007, RF023; P06, P09, P16).
    /// </summary>
    /// <param name="request">Novas preferências; <see langword="null"/> ou lista vazia significa sem filtro.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com as preferências gravadas, ou <c>400</c> (ERR-10); em erro as anteriores mantêm-se.</returns>
    [HttpPut("preferences")]
    [ProducesResponseType<SearchPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SearchPreferencesDto>> UpdatePreferences(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UpdatePreferencesRequest? request,
        CancellationToken cancellationToken)
    {
        return await this.profileService.UpdatePreferencesAsync(
            this.GetCandidateId(),
            (request ?? UpdatePreferencesRequest.Empty).ToPreferences(),
            cancellationToken);
    }

    /// <summary>
    /// Substitui as hiperligações profissionais, de 0 a 3, pela ordem indicada (6.2.6; RF012; P10).
    /// </summary>
    /// <param name="request">Endereços; a lista vazia apaga todas.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o perfil atualizado, ou <c>400</c> (ERR-10) com os endereços em erro.</returns>
    [HttpPut("links")]
    [ProducesResponseType<CandidateProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CandidateProfileDto>> SetLinks(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SetLinksRequest? request,
        CancellationToken cancellationToken)
    {
        return await this.profileService.SetLinksAsync(
            this.GetCandidateId(),
            request ?? SetLinksRequest.Empty,
            cancellationToken);
    }

    /// <summary>
    /// Guarda a fotografia de perfil, em JPEG ou PNG até 5 MB, substituindo a anterior (6.2.7; RF010; RNF015, RNF016;
    /// P01). O pedido é <c>multipart/form-data</c> com o campo <c>file</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>204</c> com o URL da fotografia no cabeçalho <c>Location</c>, <c>400</c> (ERR-11), <c>413</c> (ERR-12) ou
    /// <c>415</c> (ERR-13).
    /// </returns>
    [HttpPut("photo")]
    [UploadRequestFilter(MaxUploadRequestBytes)]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> UploadPhoto(CancellationToken cancellationToken)
    {
        IFormFile? file = await this.ReadUploadAsync(cancellationToken);
        string url = await this.profileService.UploadPhotoAsync(this.GetCandidateId(), file, cancellationToken);
        this.Response.Headers[HeaderNames.Location] = url;
        return this.NoContent();
    }

    /// <summary>
    /// Guarda o curriculum vitae, em PDF até 5 MB, substituindo o anterior (6.2.8; RF011; RNF007, RNF015, RNF016; P02).
    /// O pedido é <c>multipart/form-data</c> com o campo <c>file</c>. O curriculum vitae não tem URL.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>204</c> sem <c>Location</c>, <c>400</c> (ERR-11), <c>413</c> (ERR-12) ou <c>415</c> (ERR-13).</returns>
    [HttpPut("cv")]
    [UploadRequestFilter(MaxUploadRequestBytes)]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> UploadCv(CancellationToken cancellationToken)
    {
        IFormFile? file = await this.ReadUploadAsync(cancellationToken);
        await this.profileService.UploadCvAsync(this.GetCandidateId(), file, cancellationToken);
        return this.NoContent();
    }

    /// <summary>
    /// Obtém o curriculum vitae do próprio Candidato (6.2.9; RF011; RNF007).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o PDF, ou <c>404</c> (ERR-20) se o Candidato não tiver curriculum vitae.</returns>
    [HttpGet("cv")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCv(CancellationToken cancellationToken)
    {
        FileContent cv = await this.profileService.GetCvAsync(this.GetCandidateId(), cancellationToken);
        return this.File(cv.Content, cv.ContentType);
    }

    /// <summary>
    /// Lê o campo <c>file</c> do pedido <c>multipart/form-data</c>; o tipo de conteúdo e a dimensão já foram verificados
    /// pelo <see cref="UploadRequestFilterAttribute"/> e a falta do ficheiro é tratada pelo serviço (ERR-11).
    /// </summary>
    private async Task<IFormFile?> ReadUploadAsync(CancellationToken cancellationToken)
    {
        IFormCollection form = await this.Request.ReadFormAsync(cancellationToken);
        return form.Files.GetFile(UploadFileField);
    }

    private Guid GetCandidateId()
    {
        return this.User.TryGetAccountId(out Guid candidateId)
            ? candidateId
            : throw new InvalidOperationException("A credencial de sessão não traz o identificador da conta.");
    }
}
