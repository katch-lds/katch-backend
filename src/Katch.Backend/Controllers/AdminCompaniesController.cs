// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.Controllers;

/// <summary>
/// Supervisão das Empresas e das vagas (documentação da API v02, secção 6.11; modelo de classes do backend v02,
/// secção 6; UC16 e UC17). Só o Administrador acede (RF104); as outras contas recebem <c>403</c> (ERR-06). Esta versão
/// tem as operações 6.11.5 a 6.11.8; a consulta das Empresas pendentes e dos dados submetidos e a aprovação e a recusa
/// do registo (6.11.1 a 6.11.4) são acrescentadas pela I055.
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
    /// Suspende uma Empresa aprovada (6.11.5; RF095, RF075, RF112).
    /// </summary>
    /// <param name="companyId">Empresa a suspender.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>204</c>; <c>404</c> (ERR-20) se a Empresa não existir; <c>409</c> (ERR-30) se não estiver aprovada.</returns>
    [HttpPost("{companyId}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suspend(Guid companyId, CancellationToken cancellationToken)
    {
        await this.companyAdminService.SuspendAsync(this.GetAdminId(), companyId, cancellationToken);
        return this.NoContent();
    }

    /// <summary>
    /// Reativa uma Empresa suspensa (6.11.6; RF116, RF075, RF112).
    /// </summary>
    /// <param name="companyId">Empresa a reativar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>204</c>; <c>404</c> (ERR-20) se a Empresa não existir; <c>409</c> (ERR-30) se não estiver suspensa.</returns>
    [HttpPost("{companyId}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivate(Guid companyId, CancellationToken cancellationToken)
    {
        await this.companyAdminService.ReactivateAsync(this.GetAdminId(), companyId, cancellationToken);
        return this.NoContent();
    }

    /// <summary>
    /// Consulta a lista global de Empresas, por ordem alfabética (6.11.7; RF097).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com a designação, o estado e o número de vagas publicadas de cada Empresa.</returns>
    [HttpGet]
    [ProducesResponseType<List<CompanyAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<CompanyAdminDto>>> ListCompanies(CancellationToken cancellationToken)
    {
        return this.Ok(await this.companyAdminService.ListCompaniesAsync(cancellationToken));
    }

    /// <summary>
    /// Consulta a lista global de vagas publicadas, da publicação mais recente para a mais antiga (6.11.8; RF098).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com a função, a Empresa, a data de publicação e o número de interesses de cada vaga.</returns>
    [HttpGet("published-jobs")]
    [ProducesResponseType<List<PublishedJobAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<PublishedJobAdminDto>>> ListPublishedJobs(CancellationToken cancellationToken)
    {
        return this.Ok(await this.companyAdminService.ListPublishedJobsAsync(cancellationToken));
    }

    private Guid GetAdminId()
    {
        return this.User.TryGetAccountId(out Guid adminId)
            ? adminId
            : throw new InvalidOperationException("A credencial de sessão não traz o identificador da conta.");
    }
}
