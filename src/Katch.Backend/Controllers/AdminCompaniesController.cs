// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Katch.Backend.Controllers;

/// <summary>
/// Supervisão das Empresas e das vagas (documentação da API v02, secção 6.11; modelo de classes do backend v02, secção 6;
/// UC16). Só o Administrador acede (RF104); as outras contas recebem <c>403</c> (ERR-06). Esta versão tem a consulta das
/// Empresas pendentes e dos dados submetidos e a aprovação e a recusa do registo (6.11.1 a 6.11.4).
/// </summary>
[ApiController]
[Route("api/admin/companies")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Produces("application/json")]
public sealed class AdminCompaniesController : ControllerBase
{
    private readonly ICompanyAdminService companyAdminService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminCompaniesController"/> class.
    /// </summary>
    /// <param name="companyAdminService">Serviço da supervisão das Empresas.</param>
    public AdminCompaniesController(ICompanyAdminService companyAdminService)
    {
        this.companyAdminService = companyAdminService;
    }

    /// <summary>
    /// Consulta as Empresas pendentes de aprovação, da submissão mais antiga para a mais recente (6.11.1; RF091).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com a designação, o NIF e a data de submissão de cada Empresa pendente.</returns>
    [HttpGet("pending")]
    [ProducesResponseType<List<PendingCompanyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<PendingCompanyDto>>> ListPending(CancellationToken cancellationToken)
    {
        return this.Ok(await this.companyAdminService.ListPendingAsync(cancellationToken));
    }

    /// <summary>
    /// Consulta os dados de registo submetidos por uma Empresa, em qualquer estado (6.11.2; RF092).
    /// </summary>
    /// <param name="companyId">Empresa a consultar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com os dados submetidos; <c>404</c> (ERR-20) se a Empresa não existir.</returns>
    [HttpGet("{companyId}/registration")]
    [ProducesResponseType<CompanyRegistrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyRegistrationDto>> GetSubmittedData(Guid companyId, CancellationToken cancellationToken)
    {
        return this.Ok(await this.companyAdminService.GetSubmittedDataAsync(companyId, cancellationToken));
    }

    /// <summary>
    /// Aprova o registo de uma Empresa pendente (6.11.3; RF093, RF112).
    /// </summary>
    /// <param name="companyId">Empresa a aprovar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>204</c>; <c>404</c> (ERR-20) se a Empresa não existir; <c>409</c> (ERR-30) se não estiver pendente.</returns>
    [HttpPost("{companyId}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid companyId, CancellationToken cancellationToken)
    {
        await this.companyAdminService.ApproveAsync(this.GetAdminId(), companyId, cancellationToken);
        return this.NoContent();
    }

    /// <summary>
    /// Recusa o registo de uma Empresa pendente, com o motivo (6.11.4; RF094, RF112, P12).
    /// </summary>
    /// <param name="companyId">Empresa a recusar.</param>
    /// <param name="request">Motivo da recusa, de 10 a 500 caracteres.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>204</c>; <c>400</c> (ERR-10) se o motivo estiver em falta ou fora dos 10 a 500 caracteres; <c>404</c> (ERR-20)
    /// se a Empresa não existir; <c>409</c> (ERR-30) se não estiver pendente.
    /// </returns>
    [HttpPost("{companyId}/reject")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        Guid companyId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RejectCompanyRequest? request,
        CancellationToken cancellationToken)
    {
        await this.companyAdminService.RejectAsync(
            this.GetAdminId(),
            companyId,
            request ?? RejectCompanyRequest.Empty,
            cancellationToken);
        return this.NoContent();
    }

    private Guid GetAdminId()
    {
        return this.User.TryGetAccountId(out Guid adminId)
            ? adminId
            : throw new InvalidOperationException("A credencial de sessão não traz o identificador da conta.");
    }
}
