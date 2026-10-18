// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Arranca a API em memória com o <see cref="KatchDbContext"/> sobre SQLite in-memory
/// (Regulamento Interno, secção 11.2), sem depender do PostgreSQL.
/// </summary>
public sealed class KatchWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // O Program exige uma cadeia de ligação; esta nunca é usada, porque o contexto passa a SQLite.
        builder.UseSetting("ConnectionStrings:Katch", "Host=localhost;Database=katch_tests");
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<KatchDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<KatchDbContext>>();

            this.connection.Open();
            services.AddDbContext<KatchDbContext>(options => options.UseSqlite(this.connection));
        });
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            this.connection.Dispose();
        }
    }
}
