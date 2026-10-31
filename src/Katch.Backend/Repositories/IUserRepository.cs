// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Acesso às contas de utilizador (<c>app_user</c>) (modelo de classes do backend v02, secção 4). As restantes
/// operações do modelo (<c>ListAsync</c>, <c>CountActiveCandidatesAtAsync</c>) são acrescentadas pelas Issues que as usam.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Obtém a conta com o identificador indicado.
    /// </summary>
    /// <param name="id">Identificador da conta.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A conta, ou <see langword="null"/> se não existir.</returns>
    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém a conta com o correio eletrónico indicado, sem distinção de maiúsculas.
    /// </summary>
    /// <param name="email">Correio eletrónico.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A conta, ou <see langword="null"/> se não existir.</returns>
    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se já existe uma conta com o correio eletrónico indicado, sem distinção de maiúsculas (RF004, RF037).
    /// </summary>
    /// <param name="email">Correio eletrónico.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><see langword="true"/> se o correio já estiver registado.</returns>
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta uma conta nova, gravada no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte.
    /// </summary>
    /// <param name="user">Conta nova.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a conta foi acrescentada.</returns>
    public Task AddAsync(AppUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta a especialização de Recrutador de uma conta nova (tabela <c>recruiter</c>, chave partilhada com
    /// <c>app_user</c>), gravada no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte (RF037).
    /// </summary>
    /// <param name="recruiter">Recrutador novo, com a conta do tipo Recrutador.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando o Recrutador foi acrescentado.</returns>
    public Task AddRecruiterAsync(Recruiter recruiter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conta os Candidatos com a conta ativa no instante indicado (RF096). O estado nesse instante é o atual, exceto
    /// quando há bloqueios, suspensões ou reativações registadas a partir dele: nesse caso é o estado anterior à primeira
    /// dessas operações (modelo de classes do backend v02, secção 4; modelo de dados v02, DM-18).
    /// </summary>
    /// <param name="at">Instante, exclusivo: as operações e as contas criadas a partir dele não contam.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O número de Candidatos ativos.</returns>
    public Task<int> CountActiveCandidatesAtAsync(DateTimeOffset at, CancellationToken cancellationToken = default);
}
