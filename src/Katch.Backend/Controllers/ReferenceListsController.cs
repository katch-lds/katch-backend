// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Katch.Backend.Controllers;

/// <summary>
/// Consulta e manutenção das listas pré-definidas (documentação da API v02, secção 6.13; modelo de classes do backend
/// v02, secção 6; UC20). A escrita nas listas é reservada ao Administrador (RF104). A consulta das competências é
/// acrescentada pela Issue da gestão das competências (I062).
/// </summary>
[ApiController]
[Route("api/reference-lists")]
[Produces("application/json")]
public sealed class ReferenceListsController : ControllerBase
{
    private readonly IReferenceListService referenceListService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceListsController"/> class.
    /// </summary>
    /// <param name="referenceListService">Serviço das listas pré-definidas.</param>
    public ReferenceListsController(IReferenceListService referenceListService)
    {
        this.referenceListService = referenceListService;
    }

    /// <summary>
    /// Consulta as localidades, sem credencial: é uma operação não reservada, porque o registo do Candidato precisa da
    /// localidade antes de existir sessão (especificação de requisitos v02, secção 2.2; DAPI-12; DC-14).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com as localidades por ordem alfabética, só com o identificador e a designação.</returns>
    [HttpGet("locations")]
    [AllowAnonymous]
    [ProducesResponseType<List<ReferenceItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReferenceItemDto>>> ListLocations(CancellationToken cancellationToken)
    {
        return this.Ok(await this.referenceListService.ListLocationsAsync(cancellationToken));
    }

    /// <summary>
    /// Consulta os benefícios da lista pré-definida, com sessão iniciada de qualquer tipo de conta (RF050).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com os benefícios por ordem alfabética, ou <c>401</c> (ERR-01) sem credencial válida.</returns>
    [HttpGet("benefits")]
    [Authorize]
    [ProducesResponseType<List<ReferenceItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ReferenceItemDto>>> ListBenefits(CancellationToken cancellationToken)
    {
        return this.Ok(await this.referenceListService.ListBenefitsAsync(cancellationToken));
    }

    /// <summary>
    /// Acrescenta uma competência à lista pré-definida (UC20). A operação é registada com o Administrador (RF099,
    /// RF112).
    /// </summary>
    /// <param name="request">Designação da competência.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>201</c> com a competência criada, sem <c>Location</c> (documentação da API v02, secção 4.2), ou <c>400</c>
    /// (ERR-10) com a designação vazia, demasiado longa ou já existente.
    /// </returns>
    [HttpPost("skills")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [Consumes("application/json")]
    [ProducesResponseType<ReferenceItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReferenceItemDto>> AddSkill(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReferenceItemRequest? request,
        CancellationToken cancellationToken)
    {
        ReferenceItemDto skill = await this.referenceListService.AddSkillAsync(
            this.AdminId(),
            request ?? ReferenceItemRequest.Empty,
            cancellationToken);
        return this.StatusCode(StatusCodes.Status201Created, skill);
    }

    /// <summary>
    /// Altera a designação de uma competência (UC20). Os perfis e as vagas que a usam mantêm a associação e passam a
    /// apresentar a nova designação; a operação é registada com o Administrador (RF100, RF112).
    /// </summary>
    /// <param name="skillId">Identificador da competência.</param>
    /// <param name="request">Nova designação.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com a competência alterada, <c>400</c> (ERR-10) com a designação vazia, demasiado longa ou já
    /// existente noutra competência, ou <c>404</c> (ERR-20) se a competência não existir.
    /// </returns>
    [HttpPut("skills/{skillId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [Consumes("application/json")]
    [ProducesResponseType<ReferenceItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReferenceItemDto>> RenameSkill(
        Guid skillId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReferenceItemRequest? request,
        CancellationToken cancellationToken)
    {
        return await this.referenceListService.RenameSkillAsync(
            this.AdminId(),
            skillId,
            request ?? ReferenceItemRequest.Empty,
            cancellationToken);
    }

    /// <summary>
    /// Acrescenta um benefício à lista pré-definida (UC20). A operação é registada com o Administrador (RF101, RF112).
    /// </summary>
    /// <param name="request">Designação do benefício.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>201</c> com o benefício criado, sem <c>Location</c> (documentação da API v02, secção 4.2), ou <c>400</c>
    /// (ERR-10) com a designação vazia, demasiado longa ou já existente.
    /// </returns>
    [HttpPost("benefits")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [Consumes("application/json")]
    [ProducesResponseType<ReferenceItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReferenceItemDto>> AddBenefit(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReferenceItemRequest? request,
        CancellationToken cancellationToken)
    {
        ReferenceItemDto benefit = await this.referenceListService.AddBenefitAsync(
            this.AdminId(),
            request ?? ReferenceItemRequest.Empty,
            cancellationToken);
        return this.StatusCode(StatusCodes.Status201Created, benefit);
    }

    /// <summary>
    /// Altera a designação de um benefício (UC20). As vagas que o usam mantêm a associação e passam a apresentar a nova
    /// designação; a operação é registada com o Administrador (RF102, RF112).
    /// </summary>
    /// <param name="benefitId">Identificador do benefício.</param>
    /// <param name="request">Nova designação.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com o benefício alterado, <c>400</c> (ERR-10) com a designação vazia, demasiado longa ou já existente
    /// noutro benefício, ou <c>404</c> (ERR-20) se o benefício não existir.
    /// </returns>
    [HttpPut("benefits/{benefitId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [Consumes("application/json")]
    [ProducesResponseType<ReferenceItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReferenceItemDto>> RenameBenefit(
        Guid benefitId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReferenceItemRequest? request,
        CancellationToken cancellationToken)
    {
        return await this.referenceListService.RenameBenefitAsync(
            this.AdminId(),
            benefitId,
            request ?? ReferenceItemRequest.Empty,
            cancellationToken);
    }

    /// <summary>
    /// Identificador do Administrador, lido do <c>sub</c> da credencial de sessão (nunca da rota nem do corpo).
    /// </summary>
    private Guid AdminId()
    {
        return this.User.TryGetAccountId(out Guid adminId)
            ? adminId
            : throw new InvalidOperationException("A credencial de sessão não identifica a conta.");
    }
}
