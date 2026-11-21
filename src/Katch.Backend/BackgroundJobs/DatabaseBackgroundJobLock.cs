// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.BackgroundJobs;

/// <summary>
/// Implementação de <see cref="IBackgroundJobLock"/> com um bloqueio consultivo de sessão do PostgreSQL
/// (<c>pg_try_advisory_lock</c>), que não espera: se outra instância o tiver, a execução é saltada e repete no intervalo
/// seguinte. Noutro fornecedor (o SQLite dos testes, com uma só instância) o trabalho corre sem bloqueio.
/// </summary>
internal sealed class DatabaseBackgroundJobLock : IBackgroundJobLock
{
    private readonly KatchDbContext database;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseBackgroundJobLock"/> class.
    /// </summary>
    /// <param name="database">Contexto da base de dados do âmbito da execução, o mesmo do trabalho.</param>
    public DatabaseBackgroundJobLock(KatchDbContext database)
    {
        this.database = database;
    }

    /// <inheritdoc/>
    public async Task<int?> RunExclusiveAsync(long key, Func<Task<int>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (!this.database.Database.IsNpgsql())
        {
            return await work();
        }

        // O bloqueio de sessão pertence à ligação: abri-la aqui faz o bloqueio, o trabalho e o desbloqueio usarem a mesma.
        await this.database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            bool acquired = await this.database.Database
                .SqlQuery<bool>($"SELECT pg_try_advisory_lock({key}) AS \"Value\"")
                .SingleAsync(cancellationToken);
            if (!acquired)
            {
                return null;
            }

            try
            {
                return await work();
            }
            finally
            {
                await this.database.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({key})", CancellationToken.None);
            }
        }
        finally
        {
            await this.database.Database.CloseConnectionAsync();
        }
    }
}
