// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Security.Cryptography;
using Katch.Backend.Infrastructure;
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

    /// <summary>
    /// Gets the clock injected in place of the system clock, so that tests can control the issue time of tokens.
    /// </summary>
    public FakeClock Clock { get; } = new();

    /// <summary>
    /// Gets the interval of the periodic tasks, in seconds. By default it is one day, so that the quota restore task
    /// never runs during the tests that do not concern it; the tests of the task itself set it before the first use.
    /// </summary>
    public int BackgroundJobsIntervalSeconds { get; init; } = 86_400;

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // O Program exige uma cadeia de ligação; esta nunca é usada, porque o contexto passa a SQLite.
        builder.UseSetting("ConnectionStrings:Katch", "Host=localhost;Database=katch_tests");
        builder.UseEnvironment("Development");

        // O segredo JWT é gerado em cada execução; nunca existe um valor fixo no código.
        builder.UseSetting("Jwt:Secret", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        builder.UseSetting(
            "BackgroundJobs:IntervalSeconds",
            this.BackgroundJobsIntervalSeconds.ToString(CultureInfo.InvariantCulture));

        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(AuthorizationProbeController).Assembly);
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(this.Clock);

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
