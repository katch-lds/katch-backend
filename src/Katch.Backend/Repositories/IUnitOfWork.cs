// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.Repositories;

/// <summary>
/// Unidade de trabalho dos repositórios: grava numa só transação as alterações acumuladas no pedido (modelo de classes
/// do backend v02, secção 4).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Grava as alterações pendentes numa só transação.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O número de linhas gravadas.</returns>
    /// <exception cref="UniqueConstraintViolationException">Se a gravação violar um índice único.</exception>
    /// <exception cref="ConcurrencyConflictException">Se outro pedido alterou entretanto uma linha lida (<c>xmin</c>).</exception>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarta tudo o que o contexto do pedido tem em memória, lido ou por gravar, para repetir uma operação desde a
    /// leitura depois de um <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public void DiscardChanges();

    /// <summary>
    /// Inicia uma transação explícita, para operações com várias gravações.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A transação iniciada.</returns>
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
