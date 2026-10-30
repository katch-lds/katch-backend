// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Katch.Backend.Controllers;

/// <summary>
/// Avaliação dos Candidatos interessados nas vagas da Empresa (documentação da API v02, secções 6.6.1 a 6.6.3; modelo de
/// classes do backend v02, secção 5.1; UC11). Só para Recrutadores de Empresas aprovadas (ERR-07); o Recrutador é sempre
/// o da credencial de sessão (claim <c>sub</c>).
/// </summary>
[ApiController]
[Route("api/recruiter")]
[Authorize(Policy = AuthorizationPolicies.ApprovedCompany)]
[Produces("application/json")]
public sealed class CandidateEvaluationController : ControllerBase
{
    private const string NoStore = "no-store";

    private readonly ICandidateEvaluationService evaluationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateEvaluationController"/> class.
    /// </summary>
    /// <param name="evaluationService">Serviço da avaliação dos Candidatos.</param>
    public CandidateEvaluationController(ICandidateEvaluationService evaluationService)
    {
        this.evaluationService = evaluationService;
    }

    /// <summary>
    /// Lista os Candidatos em espera de resposta numa vaga da Empresa, do interesse mais antigo para o mais recente
    /// (6.6.1; RF060, RF113).
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com a lista, eventualmente vazia.</returns>
    [HttpGet("jobs/{jobId:guid}/waiting-candidates")]
    [ProducesResponseType<List<WaitingCandidateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<WaitingCandidateDto>>> ListWaiting(Guid jobId, CancellationToken cancellationToken)
    {
        return await this.evaluationService.ListWaitingAsync(this.GetRecruiterId(), jobId, cancellationToken);
    }

    /// <summary>
    /// Abre o perfil completo do Candidato do interesse, com as competências coincidentes com a vaga destacadas, e
    /// regista a primeira abertura (6.6.2; RF061, RF062, RF118).
    /// </summary>
    /// <param name="matchId">Identificador do interesse.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o perfil completo.</returns>
    [HttpGet("interests/{matchId:guid}/profile")]
    [ProducesResponseType<CandidateFullProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CandidateFullProfileDto>> OpenProfile(Guid matchId, CancellationToken cancellationToken)
    {
        return await this.evaluationService.OpenProfileAsync(this.GetRecruiterId(), matchId, cancellationToken);
    }

    /// <summary>
    /// Descarrega o curriculum vitae do Candidato do interesse (6.6.3; RF061; RNF007). A resposta não é guardada em
    /// cache, para que o ficheiro deixe de estar disponível quando o interesse deixar de estar em espera.
    /// </summary>
    /// <param name="matchId">Identificador do interesse.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o PDF, ou <c>404</c> (ERR-20) se o Candidato não tiver curriculum vitae.</returns>
    [HttpGet("interests/{matchId:guid}/cv")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetCv(Guid matchId, CancellationToken cancellationToken)
    {
        FileContent cv = await this.evaluationService.GetCvAsync(this.GetRecruiterId(), matchId, cancellationToken);
        this.Response.Headers[HeaderNames.CacheControl] = NoStore;
        return this.File(cv.Content, cv.ContentType, cv.FileName);
    }

    private Guid GetRecruiterId()
    {
        return this.User.TryGetAccountId(out Guid recruiterId)
            ? recruiterId
            : throw new InvalidOperationException("A credencial de sessão não traz o identificador da conta.");
    }
}
