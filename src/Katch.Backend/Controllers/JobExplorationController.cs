// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.Controllers;

/// <summary>
/// Cartão de vaga seguinte, respostas do Candidato às vagas e consulta da quota de interesses (documentação da API v02,
/// secções 6.3.1 e 6.3.4 a 6.3.6; modelo de classes do backend v02, secção 6). O detalhe da vaga e a página da Empresa
/// (6.3.2 e 6.3.3) são acrescentados por outras Issues. O Candidato é sempre o da credencial de sessão (claim <c>sub</c>), nunca o do pedido.
/// </summary>
[ApiController]
[Route("api/candidate/jobs")]
[Authorize(Policy = AuthorizationPolicies.Candidate)]
[Produces("application/json")]
public sealed class JobExplorationController : ControllerBase
{
    private readonly IJobExplorationService jobExploration;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExplorationController"/> class.
    /// </summary>
    /// <param name="jobExploration">Serviço de exploração de vagas.</param>
    public JobExplorationController(IJobExplorationService jobExploration)
    {
        this.jobExploration = jobExploration;
    }

    /// <summary>
    /// Obtém o cartão da vaga seguinte que cumpre o filtro do RF014, com a distância à localidade do Candidato (RF013 a
    /// RF015). Os cartões são apresentados um de cada vez: depois de o Candidato responder, o pedido seguinte traz outra
    /// vaga.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o cartão, ou <c>204</c> se nenhuma vaga cumprir o filtro (UC05, A6).</returns>
    [HttpGet("next")]
    [ProducesResponseType<JobCardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<JobCardDto>> GetNextCard(CancellationToken cancellationToken)
    {
        JobCardDto? card = await this.jobExploration.GetNextCardAsync(this.GetCandidateId(), cancellationToken);
        if (card is null)
        {
            return this.NoContent();
        }

        return this.Ok(card);
    }

    /// <summary>
    /// Recusa uma vaga (RF018, RF112): fica registada a recusa e a data, sem consumir a quota e sem limite de recusas.
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>204</c>, ou <c>404</c> (ERR-20) se a vaga não existir, <c>409</c> (ERR-32) se já respondeu à vaga, ou
    /// <c>409</c> (ERR-33) se a vaga já não está disponível.
    /// </returns>
    [HttpPost("{jobId}/decline")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Decline(Guid jobId, CancellationToken cancellationToken)
    {
        await this.jobExploration.DeclineAsync(this.GetCandidateId(), jobId, cancellationToken);
        return this.NoContent();
    }

    /// <summary>
    /// Manifesta interesse numa vaga (RF019, RF020, RF112, RF113): numa só transação regista o interesse em espera e
    /// consome um interesse da quota. Se este interesse esgotar a quota, <c>blockedUntil</c> traz o fim do bloqueio.
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com a quota atualizada, ou <c>404</c> (ERR-20), <c>409</c> (ERR-31) se a quota está esgotada,
    /// <c>409</c> (ERR-32) se já respondeu à vaga, ou <c>409</c> (ERR-33) se a vaga já não está disponível.
    /// </returns>
    [HttpPost("{jobId}/interest")]
    [ProducesResponseType<QuotaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuotaDto>> ExpressInterest(Guid jobId, CancellationToken cancellationToken)
    {
        QuotaDto quota = await this.jobExploration.ExpressInterestAsync(this.GetCandidateId(), jobId, cancellationToken);
        return this.Ok(quota);
    }

    /// <summary>
    /// Consulta os interesses disponíveis (RF020 a RF022): se o bloqueio já terminou, a quota é reposta antes de responder.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com a quota: disponíveis (0 a 10), total (10) e fim do bloqueio (<c>null</c> fora dele).</returns>
    [HttpGet("quota")]
    [ProducesResponseType<QuotaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<QuotaDto>> GetQuota(CancellationToken cancellationToken)
    {
        QuotaDto quota = await this.jobExploration.GetQuotaAsync(this.GetCandidateId(), cancellationToken);
        return this.Ok(quota);
    }

    private Guid GetCandidateId()
    {
        return Guid.TryParse(this.User.FindFirst("sub")?.Value, out Guid candidateId)
            ? candidateId
            : throw new InvalidOperationException("A credencial de sessão não traz o identificador da conta.");
    }
}
