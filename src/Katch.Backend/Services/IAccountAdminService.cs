// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Administração das contas de utilizador pelo Administrador (modelo de classes do backend v02, secção 5.1; documentação
/// da API v02, secção 6.10; UC18).
/// </summary>
public interface IAccountAdminService
{
    /// <summary>
    /// Lista todas as contas por ordem alfabética do correio eletrónico (RF085).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As contas, com o correio, o tipo e o estado.</returns>
    public Task<List<AccountDto>> ListAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista todos os Candidatos por ordem alfabética do nome (RF088).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os Candidatos, com o nome, o correio, a localidade, a data de registo e o estado da conta.</returns>
    public Task<List<CandidateAdminDto>> ListCandidatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bloqueia uma conta de Candidato ou de Recrutador ativa e passa as conversas abertas da conta a consulta, com o
    /// registo <c>ACCOUNT_BLOCKED</c>, numa única gravação (RF086, RF075, RF112).
    /// </summary>
    /// <param name="adminId">Administrador que executa a operação.</param>
    /// <param name="userId">Conta a bloquear.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a conta foi bloqueada.</returns>
    /// <exception cref="ElementNotFoundException">A conta não existe.</exception>
    /// <exception cref="OperationConflictException">A conta é de Administrador ou não está ativa (ERR-30).</exception>
    public Task BlockAsync(Guid adminId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspende a conta ativa de um Candidato e passa as suas conversas abertas a consulta, com o registo
    /// <c>ACCOUNT_SUSPENDED</c>, numa única gravação (RF089, RF075, RF112).
    /// </summary>
    /// <param name="adminId">Administrador que executa a operação.</param>
    /// <param name="candidateId">Conta do Candidato a suspender.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a conta foi suspensa.</returns>
    /// <exception cref="ElementNotFoundException">A conta não existe ou não é de Candidato.</exception>
    /// <exception cref="OperationConflictException">A conta não está ativa (ERR-30).</exception>
    public Task SuspendCandidateAsync(Guid adminId, Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reativa uma conta bloqueada ou suspensa, com o registo <c>ACCOUNT_REACTIVATED</c> (RF087, RF090, RF112). As
    /// conversas mantêm-se em modo apenas de consulta (RF075).
    /// </summary>
    /// <param name="adminId">Administrador que executa a operação.</param>
    /// <param name="userId">Conta a reativar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a conta foi reativada.</returns>
    /// <exception cref="ElementNotFoundException">A conta não existe.</exception>
    /// <exception cref="OperationConflictException">A conta já está ativa (ERR-30).</exception>
    public Task ReactivateAsync(Guid adminId, Guid userId, CancellationToken cancellationToken = default);
}
