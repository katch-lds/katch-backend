// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.Controllers;

/// <summary>
/// Administração das contas de utilizador (documentação da API v02, secção 6.10; modelo de classes do backend v02,
/// secção 6; UC18). Só o Administrador acede (RF104); as outras contas recebem <c>403</c> (ERR-06).
/// </summary>
[ApiController]
[Route("api/admin/accounts")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Produces("application/json")]
public sealed class AdminAccountsController : ControllerBase
{
    private readonly IAccountAdminService accountAdminService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminAccountsController"/> class.
    /// </summary>
    /// <param name="accountAdminService">Serviço da administração das contas.</param>
    public AdminAccountsController(IAccountAdminService accountAdminService)
    {
        this.accountAdminService = accountAdminService;
    }

    /// <summary>
    /// Consulta todas as contas de utilizador, por ordem alfabética do correio eletrónico (6.10.1; RF085).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o correio, o tipo e o estado de cada conta.</returns>
    [HttpGet]
    [ProducesResponseType<List<AccountDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<AccountDto>>> ListAccounts(CancellationToken cancellationToken)
    {
        return this.Ok(await this.accountAdminService.ListAccountsAsync(cancellationToken));
    }

    /// <summary>
    /// Consulta todos os Candidatos, por ordem alfabética do nome (6.10.2; RF088).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o nome, o correio, a localidade, a data de registo e o estado de cada Candidato.</returns>
    [HttpGet("candidates")]
    [ProducesResponseType<List<CandidateAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<CandidateAdminDto>>> ListCandidates(CancellationToken cancellationToken)
    {
        return this.Ok(await this.accountAdminService.ListCandidatesAsync(cancellationToken));
    }

    /// <summary>
    /// Bloqueia uma conta de Candidato ou de Recrutador (6.10.3; RF086, RF075, RF112).
    /// </summary>
    /// <param name="userId">Conta a bloquear.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>204</c>; <c>404</c> (ERR-20) se a conta não existir; <c>409</c> (ERR-30) se for de Administrador ou não estiver
    /// ativa.
    /// </returns>
    [HttpPost("{userId}/block")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Block(Guid userId, CancellationToken cancellationToken)
    {
        await this.accountAdminService.BlockAsync(this.GetAdminId(), userId, cancellationToken);
        return this.NoContent();
    }

    /// <summary>
    /// Suspende a conta de um Candidato (6.10.4; RF089, RF075, RF112).
    /// </summary>
    /// <param name="candidateId">Conta do Candidato a suspender.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>204</c>; <c>404</c> (ERR-20) se a conta não existir ou não for de Candidato; <c>409</c> (ERR-30) se não estiver
    /// ativa.
    /// </returns>
    [HttpPost("candidates/{candidateId}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SuspendCandidate(Guid candidateId, CancellationToken cancellationToken)
    {
        await this.accountAdminService.SuspendCandidateAsync(this.GetAdminId(), candidateId, cancellationToken);
        return this.NoContent();
    }

    /// <summary>
    /// Reativa uma conta bloqueada ou suspensa (6.10.5; RF087, RF090, RF075, RF112).
    /// </summary>
    /// <param name="userId">Conta a reativar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>204</c>; <c>404</c> (ERR-20) se a conta não existir; <c>409</c> (ERR-30) se já estiver ativa.</returns>
    [HttpPost("{userId}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivate(Guid userId, CancellationToken cancellationToken)
    {
        await this.accountAdminService.ReactivateAsync(this.GetAdminId(), userId, cancellationToken);
        return this.NoContent();
    }

    private Guid GetAdminId()
    {
        return this.User.TryGetAccountId(out Guid adminId)
            ? adminId
            : throw new InvalidOperationException("A credencial de sessão não traz o identificador da conta.");
    }
}
