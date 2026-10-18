// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Katch.Backend.Infrastructure.Persistence;

/// <summary>
/// Cria o <see cref="KatchDbContext"/> para os comandos <c>dotnet ef</c>, sem arrancar a aplicação.
/// </summary>
/// <remarks>
/// A cadeia de ligação vem da variável de ambiente <c>ConnectionStrings__Katch</c>. Sem ela, usa-se uma cadeia
/// sem credenciais, suficiente para gerar migrations; aplicar ou reverter migrations exige a variável definida.
/// </remarks>
public sealed class KatchDbContextDesignTimeFactory : IDesignTimeDbContextFactory<KatchDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Katch";

    private const string MigrationsOnlyConnectionString = "Host=localhost;Port=6000;Database=katch";

    /// <inheritdoc/>
    public KatchDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable)
            ?? MigrationsOnlyConnectionString;

        var options = new DbContextOptionsBuilder<KatchDbContext>();
        options.UseKatchPostgres(connectionString);
        return new KatchDbContext(options.Options);
    }
}
