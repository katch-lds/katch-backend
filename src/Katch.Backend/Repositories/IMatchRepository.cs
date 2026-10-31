// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Acesso às respostas dos Candidatos às vagas (<c>match</c>) (modelo de classes do backend v02, secção 4). As restantes
/// operações do modelo são acrescentadas pelas Issues da avaliação, dos matches e das conversas.
/// </summary>
public interface IMatchRepository
{
    /// <summary>
    /// Obtém a resposta de um Candidato a uma vaga.
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A resposta, ou <see langword="null"/> se o Candidato ainda não respondeu à vaga.</returns>
    public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta uma resposta nova, gravada no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte.
    /// </summary>
    /// <param name="match">Resposta nova.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a resposta foi acrescentada.</returns>
    public Task AddAsync(Match match, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conta os interesses manifestados no período: respostas com estado diferente de recusada pelo Candidato, pela data
    /// da resposta (RF096; modelo de dados v02, DM-21).
    /// </summary>
    /// <param name="periodStart">Início do período, inclusive.</param>
    /// <param name="periodEnd">Fim do período, exclusivo.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O número de interesses.</returns>
    public Task<int> CountInterestsAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conta os matches confirmados no período, pela data da aceitação pelo Recrutador (RF096; modelo de dados v02,
    /// DM-24).
    /// </summary>
    /// <param name="periodStart">Início do período, inclusive.</param>
    /// <param name="periodEnd">Fim do período, exclusivo.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O número de matches.</returns>
    public Task<int> CountMatchesAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default);
}
