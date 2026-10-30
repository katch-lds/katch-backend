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

    /// <summary>
    /// Obtém um match pelo identificador, acompanhado pelo contexto.
    /// </summary>
    /// <param name="id">Identificador do match, que é também o da conversa.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O match, ou <see langword="null"/> se não existir.</returns>
    public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o Recrutador da Empresa dona da vaga do match, a parte do lado da Empresa na conversa (RF026).
    /// </summary>
    /// <param name="matchId">Identificador do match.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O identificador do Recrutador, ou <see langword="null"/> se o match não existir.</returns>
    public Task<Guid?> GetRecruiterIdAsync(Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista os matches confirmados do Candidato, com os dados da vaga e da Empresa, numa só consulta (RF024, RF025).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os matches no estado <c>MATCHED</c>, sem ordem definida.</returns>
    public Task<List<MatchView>> ListMatchViewsByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista os matches confirmados das vagas de uma Empresa, com os dados da vaga e do Candidato, numa só consulta
    /// (RF067).
    /// </summary>
    /// <param name="companyId">Identificador da Empresa.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os matches no estado <c>MATCHED</c>, sem ordem definida.</returns>
    public Task<List<MatchView>> ListMatchViewsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as conversas de uma conta, abertas e em modo apenas de consulta, como Candidato ou como Recrutador da
    /// Empresa da vaga, numa só consulta (RF027, RF029).
    /// </summary>
    /// <param name="userId">Identificador da conta.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As conversas, sem ordem definida.</returns>
    public Task<List<ConversationView>> ListConversationsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
