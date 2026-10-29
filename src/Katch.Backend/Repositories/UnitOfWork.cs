// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="IUnitOfWork"/> sobre o <see cref="KatchDbContext"/> do pedido.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private const string UniqueViolation = "23505";

    private const string CheckViolation = "23514";

    private readonly KatchDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWork"/> class.
    /// </summary>
    /// <param name="context">Contexto da base de dados do pedido.</param>
    public UnitOfWork(KatchDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: UniqueViolation } postgres)
        {
            throw new UniqueConstraintViolationException(postgres.ConstraintName ?? string.Empty, exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: CheckViolation } postgres)
        {
            throw new CheckConstraintViolationException(postgres.ConstraintName ?? string.Empty, exception);
        }
    }

    /// <inheritdoc/>
    public void DiscardChanges()
    {
        this.context.ChangeTracker.Clear();
    }

    /// <inheritdoc/>
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return this.context.Database.BeginTransactionAsync(cancellationToken);
    }
}
