// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Exploração de vagas pelo Candidato (modelo de classes do backend v02, secção 5). Tem o cartão de vaga seguinte, as
/// respostas à vaga e a quota; o detalhe e a página da Empresa são acrescentados por outras Issues.
/// </summary>
public interface IJobExplorationService
{
    /// <summary>
    /// Obtém o cartão da vaga seguinte que cumpre o filtro do RF014 para o Candidato, com a distância à sua localidade
    /// (RF013 a RF015). Os cartões são apresentados um de cada vez: depois de o Candidato responder, o pedido seguinte traz
    /// outra vaga, e uma vaga recusada ou com interesse não volta a ser apresentada. Com a quota esgotada, os cartões
    /// continuam a ser apresentados (UC05, A4).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O cartão, ou <see langword="null"/> se nenhuma vaga cumprir o filtro (UC05, A6).</returns>
    public Task<JobCardDto?> GetNextCardAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Regista a recusa da vaga, sem consumir a quota e sem limite de recusas (RF018, RF112).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a recusa está gravada.</returns>
    /// <exception cref="JobAnswerRejectedException">Se a vaga não existir, já tiver resposta ou já não estiver disponível.</exception>
    public Task DeclineAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Regista o interesse na vaga e consome um interesse da quota, numa só gravação (RF019, RF020, RF112, RF113).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A quota atualizada.</returns>
    /// <exception cref="JobAnswerRejectedException">Se a vaga não existir, já tiver resposta ou já não estiver disponível.</exception>
    /// <exception cref="Domain.QuotaExhaustedException">Se a quota estiver esgotada e o bloqueio não tiver terminado.</exception>
    public Task<QuotaDto> ExpressInterestAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta a quota de interesses, repondo-a antes se o período de bloqueio já terminou (RF020 a RF022).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A quota atual.</returns>
    public Task<QuotaDto> GetQuotaAsync(Guid candidateId, CancellationToken cancellationToken = default);
}
