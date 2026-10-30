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
    /// Lista os matches das vagas de uma Empresa, acompanhados pelo contexto, para passar as conversas abertas a consulta
    /// quando a Empresa é suspensa (RF075, RF095). As alterações são gravadas no
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte.
    /// </summary>
    /// <param name="companyId">Identificador da Empresa.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os registos no estado <c>MATCHED</c> das vagas da Empresa.</returns>
    public Task<List<Match>> ListMatchesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as conversas abertas de uma conta, como Candidato ou como Recrutador da Empresa da vaga, acompanhadas pelo
    /// contexto, para as passar a consulta quando a conta é bloqueada ou suspensa (RF075). As alterações são gravadas no
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte.
    /// </summary>
    /// <param name="userId">Identificador da conta.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os registos no estado <c>MATCHED</c> com a conversa aberta em que a conta é uma das partes.</returns>
    public Task<List<Match>> ListOpenConversationsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
