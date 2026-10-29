// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Katch.Backend.Controllers;

/// <summary>
/// Competências do perfil profissional do Candidato (documentação da API v02, secções 6.2.4, 6.2.5, 6.2.10 e 6.2.11;
/// modelo de classes do backend v02, secção 6). As restantes operações do controller são acrescentadas pela Issue do
/// perfil (I113). O Candidato é sempre o da credencial de sessão (claim <c>sub</c>), nunca o do pedido.
/// </summary>
[ApiController]
[Route("api/candidate/profile")]
[Authorize(Policy = AuthorizationPolicies.Candidate)]
[Produces("application/json")]
public sealed class CandidateProfileController : ControllerBase
{
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
    /// Associa ao perfil uma competência da lista pré-definida (RF008).
    /// </summary>
    /// <param name="request">Competência a acrescentar; sem corpo, o campo é indicado como obrigatório.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>201</c> com o perfil atualizado, ou <c>400</c> (ERR-10) se <c>skillId</c> faltar, não pertencer à lista ou já
    /// estiver associada ao perfil.
    /// </returns>
    [HttpPost("skills")]
    [ProducesResponseType<CandidateProfileDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CandidateProfileDto>> AddSkill(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AddSkillRequest? request,
        CancellationToken cancellationToken)
    {
        CandidateProfileDto profile = await this.profileService.AddSkillAsync(
            this.GetCandidateId(),
            request ?? AddSkillRequest.Empty,
            cancellationToken);
        return this.StatusCode(StatusCodes.Status201Created, profile);
    }

    /// <summary>
    /// Retira do perfil uma competência da lista pré-definida (RF008).
    /// </summary>
    /// <param name="skillId">Identificador da competência associada ao perfil.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>204</c>, ou <c>404</c> (ERR-20) se a competência não estiver associada ao perfil.</returns>
    [HttpDelete("skills/{skillId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSkill(Guid skillId, CancellationToken cancellationToken)
    {
        await this.profileService.RemoveSkillAsync(this.GetCandidateId(), skillId, cancellationToken);
        return this.NoContent();
    }

    /// <summary>
    /// Acrescenta ao perfil uma competência da opção «Outro», validada no servidor segundo o P07 (RF009).
    /// </summary>
    /// <param name="request">Designação da competência; sem corpo, o campo é indicado como obrigatório.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>201</c> com o perfil atualizado, ou <c>400</c> (ERR-10) se <c>label</c> faltar, não cumprir o P07 ou já
    /// estiver indicada no perfil.
    /// </returns>
    [HttpPost("custom-skills")]
    [ProducesResponseType<CandidateProfileDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CandidateProfileDto>> AddCustomSkill(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AddCustomSkillRequest? request,
        CancellationToken cancellationToken)
    {
        CandidateProfileDto profile = await this.profileService.AddCustomSkillAsync(
            this.GetCandidateId(),
            request ?? AddCustomSkillRequest.Empty,
            cancellationToken);
        return this.StatusCode(StatusCodes.Status201Created, profile);
    }

    /// <summary>
    /// Retira do perfil uma competência da opção «Outro» (RF009).
    /// </summary>
    /// <param name="customSkillId">Identificador da competência «Outro».</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>204</c>, ou <c>404</c> (ERR-20) se a competência «Outro» não existir no perfil.</returns>
    [HttpDelete("custom-skills/{customSkillId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCustomSkill(Guid customSkillId, CancellationToken cancellationToken)
    {
        await this.profileService.RemoveCustomSkillAsync(this.GetCandidateId(), customSkillId, cancellationToken);
        return this.NoContent();
    }

    private Guid GetCandidateId()
    {
        return this.User.TryGetAccountId(out Guid candidateId)
            ? candidateId
            : throw new InvalidOperationException("A credencial de sessão não traz o identificador da conta.");
    }
}
