// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Supervisão das Empresas e das vagas pelo Administrador (modelo de classes do backend v02, secção 5.1; documentação da
/// API v02, secção 6.11; UC16 e UC17). Esta versão tem a suspensão e a reativação da Empresa e as listas globais
/// (6.11.5 a 6.11.8); a aprovação e a recusa do registo (6.11.1 a 6.11.4) são acrescentadas pela I055.
/// </summary>
public interface ICompanyAdminService
{
    /// <summary>
    /// Suspende uma Empresa aprovada e passa as conversas abertas das suas vagas a consulta, com o registo
    /// <c>COMPANY_SUSPENDED</c>, numa única gravação (RF095, RF075, RF112). As vagas da Empresa deixam de ser
    /// apresentadas aos Candidatos (RF014) e os matches existentes continuam registados.
    /// </summary>
    /// <param name="adminId">Administrador que executa a operação.</param>
    /// <param name="companyId">Empresa a suspender.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a Empresa foi suspensa.</returns>
    /// <exception cref="ElementNotFoundException">A Empresa não existe.</exception>
    /// <exception cref="OperationConflictException">A Empresa não está aprovada (ERR-30).</exception>
    public Task SuspendAsync(Guid adminId, Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reativa uma Empresa suspensa, que volta a aprovada, com o registo <c>COMPANY_REACTIVATED</c> (RF116, RF112). As
    /// conversas mantêm-se em modo apenas de consulta (RF075).
    /// </summary>
    /// <param name="adminId">Administrador que executa a operação.</param>
    /// <param name="companyId">Empresa a reativar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a Empresa foi reativada.</returns>
    /// <exception cref="ElementNotFoundException">A Empresa não existe.</exception>
    /// <exception cref="OperationConflictException">A Empresa não está suspensa (ERR-30).</exception>
    public Task ReactivateAsync(Guid adminId, Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista todas as Empresas, em qualquer estado, por ordem alfabética da designação, com o número de vagas publicadas
    /// (RF097).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As Empresas.</returns>
    public Task<List<CompanyAdminDto>> ListCompaniesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as vagas publicadas de todas as Empresas, da publicação mais recente para a mais antiga, com o número de
    /// interesses (RF098).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As vagas publicadas.</returns>
    public Task<List<PublishedJobAdminDto>> ListPublishedJobsAsync(CancellationToken cancellationToken = default);
}
