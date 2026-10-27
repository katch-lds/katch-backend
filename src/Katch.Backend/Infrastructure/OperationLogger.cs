// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Implementação de <see cref="IOperationLogger"/> (modelo de classes do backend v02, secção 5.2): acrescenta a linha
/// ao <c>operation_log</c> com o instante do <see cref="IClock"/>. A gravação é feita pelo serviço, na mesma gravação da
/// operação registada (RF112).
/// </summary>
public sealed class OperationLogger : IOperationLogger
{
    private readonly IOperationLogRepository repository;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationLogger"/> class.
    /// </summary>
    /// <param name="repository">Repositório do <c>operation_log</c>.</param>
    /// <param name="clock">Relógio do instante da operação.</param>
    public OperationLogger(IOperationLogRepository repository, IClock clock)
    {
        this.repository = repository;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public Task LogAsync(
        Guid? userId,
        OperationType operation,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        return this.repository.AddAsync(OperationLog.Record(userId, operation, entityId, this.clock.UtcNow), cancellationToken);
    }
}
