// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Acesso ao registo de autoria das operações (<c>operation_log</c>), que só recebe inserções (modelo de classes do
/// backend v02, secção 4; modelo de dados v02, secção 6.15).
/// </summary>
public interface IOperationLogRepository
{
    /// <summary>
    /// Acrescenta uma linha nova, gravada no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte.
    /// </summary>
    /// <param name="entry">Linha nova.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a linha foi acrescentada.</returns>
    public Task AddAsync(OperationLog entry, CancellationToken cancellationToken = default);
}
