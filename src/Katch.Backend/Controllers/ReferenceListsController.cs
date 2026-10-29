// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.Controllers;

/// <summary>
/// Consulta das listas pré-definidas (documentação da API v02, secção 6.13; modelo de classes do backend v02,
/// secção 6). A consulta dos benefícios e a escrita pelo Administrador são acrescentadas pela Issue dessas operações
/// (I118).
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
    /// Consulta as competências da lista pré-definida, com sessão iniciada de qualquer tipo de conta (RF008, RF050).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com as competências por ordem alfabética, ou <c>401</c> (ERR-01) sem credencial válida.</returns>
    [HttpGet("skills")]
    [Authorize]
    [ProducesResponseType<List<ReferenceItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ReferenceItemDto>>> ListSkills(CancellationToken cancellationToken)
    {
        return this.Ok(await this.referenceListService.ListSkillsAsync(cancellationToken));
    }
}
