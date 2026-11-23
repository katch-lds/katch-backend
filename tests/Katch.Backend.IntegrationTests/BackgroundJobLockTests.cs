// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.BackgroundJobs;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes do bloqueio das tarefas periódicas entre instâncias (ROB-007). O teste em PostgreSQL só corre com a variável
/// de ambiente <see cref="PostgresVariable"/> (cadeia de ligação a uma base descartável), porque o bloqueio consultivo
/// não existe em SQLite.
/// </summary>
public sealed class BackgroundJobLockTests : IClassFixture<KatchWebApplicationFactory>
{
    /// <summary>
    /// Variável de ambiente com a cadeia de ligação ao PostgreSQL dos testes.
    /// </summary>
    public const string PostgresVariable = "KATCH_TEST_POSTGRES";

    private const long Key = 7_199_999;

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundJobLockTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public BackgroundJobLockTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Em SQLite (uma só instância) o trabalho corre sempre e o resultado é devolvido.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WithoutPostgresTheWorkAlwaysRuns()
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        IBackgroundJobLock jobLock = scope.ServiceProvider.GetRequiredService<IBackgroundJobLock>();

        int? result = await jobLock.RunExclusiveAsync(Key, () => Task.FromResult(3), TestContext.Current.CancellationToken);

        Assert.Equal(3, result);
    }

    /// <summary>
    /// Em PostgreSQL, enquanto uma instância executa a tarefa, outra é saltada; depois de libertado, volta a correr.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WithPostgresASecondInstanceIsSkippedWhileTheFirstRuns()
    {
        string? connectionString = Environment.GetEnvironmentVariable(PostgresVariable);
        Assert.SkipWhen(string.IsNullOrEmpty(connectionString), $"Sem {PostgresVariable}: o bloqueio consultivo só existe em PostgreSQL.");
        CancellationToken token = TestContext.Current.CancellationToken;
        await using KatchDbContext first = CreatePostgresContext(connectionString!);
        await using KatchDbContext second = CreatePostgresContext(connectionString!);
        var firstLock = new DatabaseBackgroundJobLock(first);
        var secondLock = new DatabaseBackgroundJobLock(second);
        int? whileHeld = 0;

        int? firstResult = await firstLock.RunExclusiveAsync(
            Key,
            async () =>
            {
                whileHeld = await secondLock.RunExclusiveAsync(Key, () => Task.FromResult(2), token);
                return 1;
            },
            token);
        int? afterRelease = await secondLock.RunExclusiveAsync(Key, () => Task.FromResult(2), token);

        Assert.Equal(1, firstResult);
        Assert.Null(whileHeld);
        Assert.Equal(2, afterRelease);
    }

    private static KatchDbContext CreatePostgresContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<KatchDbContext>();
        options.UseKatchPostgres(connectionString);
        return new KatchDbContext(options.Options);
    }
}
