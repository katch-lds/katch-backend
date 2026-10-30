// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.Controllers;

/// <summary>
/// Consulta dos matches confirmados (documentação da API v02, secção 6.7.1; modelo de classes do backend v02, secção 6;
/// RF024, RF025, RF067). A conta é sempre a da credencial de sessão (claim <c>sub</c>), nunca a do pedido.
/// </summary>
[ApiController]
[Route("api/matches")]
[Authorize(Policy = AuthorizationPolicies.CandidateOrRecruiter)]
[Produces("application/json")]
public sealed class MatchesController : ControllerBase
{
    private readonly IMatchService matchService;
    private readonly IAuthorizationService authorization;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchesController"/> class.
    /// </summary>
    /// <param name="matchService">Serviço de consulta dos matches.</param>
    /// <param name="authorization">Avaliação das políticas de autorização, para exigir a Empresa aprovada ao Recrutador.</param>
    public MatchesController(IMatchService matchService, IAuthorizationService authorization)
    {
        this.matchService = matchService;
        this.authorization = authorization;
    }

    /// <summary>
    /// Lista os matches confirmados do Candidato (RF024, RF025) ou das vagas da Empresa do Recrutador (RF067), do mais
    /// recente para o mais antigo. Ao Recrutador exige-se a Empresa aprovada; o Candidato continua a ver os matches de
    /// Empresas suspensas.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com os matches, <c>401</c> (ERR-01) sem sessão, ou <c>403</c> (ERR-06) para o Administrador e (ERR-07)
    /// para o Recrutador com a Empresa não aprovada.
    /// </returns>
    [HttpGet]
    [ProducesResponseType<List<MatchDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<MatchDto>>> List(CancellationToken cancellationToken)
    {
        Guid accountId = this.GetAccountId();

        if (this.User.IsInRole(AccountRoles.Candidate))
        {
            return await this.matchService.ListForCandidateAsync(accountId, cancellationToken);
        }

        // A política ApprovedCompany só se aplica ao Recrutador: avalia-se aqui, para que o Candidato fique de fora.
        AuthorizationResult approved = await this.authorization.AuthorizeAsync(this.User, AuthorizationPolicies.ApprovedCompany);
        if (!approved.Succeeded)
        {
            AuthorizationFailureReason? reason = approved.Failure?.FailureReasons
                .FirstOrDefault(failure => failure.Handler is ApprovedCompanyAuthorizationHandler);
            return this.StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiErrorDto(
                    ApiErrorResponse.CompanyNotApprovedCode,
                    reason?.Message ?? ApiErrorResponse.CompanyNotApprovedMessage(null)));
        }

        return await this.matchService.ListForRecruiterAsync(accountId, cancellationToken);
    }

    private Guid GetAccountId()
    {
        return this.User.TryGetAccountId(out Guid accountId)
            ? accountId
            : throw new InvalidOperationException("A credencial de sessão não identifica a conta.");
    }
}
