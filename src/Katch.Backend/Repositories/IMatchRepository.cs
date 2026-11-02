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
    /// Indica se o Candidato tem um interesse registado numa vaga da Empresa do Recrutador, isto é, uma resposta que não
    /// seja a recusa da vaga pelo Candidato (RF061). A ligação do Recrutador à Empresa e às vagas é feita na consulta.
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="recruiterId">Identificador do Recrutador.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><see langword="true"/> se existir esse interesse.</returns>
    public Task<bool> HasInterestFromCandidateInCompanyAsync(Guid candidateId, Guid recruiterId, CancellationToken cancellationToken = default);
}
