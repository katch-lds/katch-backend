// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Acesso às Empresas (<c>company</c>) (modelo de classes do backend v02, secção 4). As restantes operações do modelo
/// (<c>ListAllWithPublishedCountAsync</c>, <c>CountByStatusAtAsync</c>) são acrescentadas pelas Issues que as usam.
/// </summary>
public interface ICompanyRepository
{
    /// <summary>
    /// Obtém a Empresa representada pelo Recrutador indicado.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A Empresa, ou <see langword="null"/> se o Recrutador ainda não a registou.</returns>
    public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se o NIF já está registado noutra Empresa (RF042).
    /// </summary>
    /// <param name="taxId">Número de identificação fiscal.</param>
    /// <param name="exceptId">Empresa a ignorar (a própria, na nova submissão), ou <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><see langword="true"/> se outra Empresa tiver o mesmo NIF.</returns>
    public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta uma Empresa nova, gravada no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte.
    /// </summary>
    /// <param name="company">Empresa nova.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a Empresa foi acrescentada.</returns>
    public Task AddAsync(Company company, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma Empresa, acompanhada pelo contexto: as alterações são gravadas no
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte.
    /// </summary>
    /// <param name="id">Identificador da Empresa.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A Empresa, ou <see langword="null"/> se não existir.</returns>
    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma Empresa com a sua localidade, só para leitura (RF092).
    /// </summary>
    /// <param name="id">Identificador da Empresa.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A Empresa com a localidade, ou <see langword="null"/> se a Empresa não existir.</returns>
    public Task<CompanyWithLocation?> GetWithLocationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as Empresas pendentes de aprovação, só para leitura (RF091).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As Empresas no estado pendente, sem ordem garantida.</returns>
    public Task<List<Company>> ListPendingAsync(CancellationToken cancellationToken = default);
}
