// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Katch.Backend.Infrastructure.Hosting;

/// <summary>
/// Verificação de prontidão (<c>/health/ready</c>): a API está pronta quando consegue ligar à base de dados.
/// </summary>
internal sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly KatchDbContext database;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseHealthCheck"/> class.
    /// </summary>
    /// <param name="database">Contexto da base de dados.</param>
    public DatabaseHealthCheck(KatchDbContext database)
    {
        this.database = database;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return await this.database.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Sem ligação à base de dados.");
    }
}
