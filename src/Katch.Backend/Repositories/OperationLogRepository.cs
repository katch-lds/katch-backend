// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="IOperationLogRepository"/> sobre o <see cref="KatchDbContext"/>.
/// </summary>
public sealed class OperationLogRepository : IOperationLogRepository
{
    private readonly KatchDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationLogRepository"/> class.
    /// </summary>
    /// <param name="context">Contexto da base de dados do pedido.</param>
    public OperationLogRepository(KatchDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public async Task AddAsync(OperationLog entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await this.context.OperationLogs.AddAsync(entry, cancellationToken);
    }
}
