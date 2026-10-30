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
    /// Obtém uma resposta pelo identificador, acompanhada pelo contexto: as alterações (por exemplo, o registo da
    /// abertura do perfil) são gravadas no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte.
    /// </summary>
    /// <param name="id">Identificador da resposta (<c>match.id</c>).</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A resposta, ou <see langword="null"/> se não existir.</returns>
    public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista os interesses em espera (<see cref="Domain.Enums.MatchStatus.Waiting"/>) de uma vaga, com os dados do
    /// Candidato apresentados na lista de candidatos em espera, do interesse mais antigo para o mais recente (RF060,
    /// RF113). As recusas da vaga e os interesses já decididos não são devolvidos. Só para leitura.
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os interesses em espera da vaga; lista vazia se não houver.</returns>
    public Task<List<WaitingInterest>> ListWaitingByJobAsync(Guid jobId, CancellationToken cancellationToken = default);

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
