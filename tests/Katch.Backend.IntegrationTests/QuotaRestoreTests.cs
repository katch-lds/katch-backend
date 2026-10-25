// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.BackgroundJobs;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes da reposição da quota de interesses sobre SQLite in-memory: a consulta dos Candidatos com o bloqueio terminado,
/// o <see cref="IQuotaService"/> e a tarefa periódica no arranque real da API (RF021, P11). A marca de concorrência
/// <c>xmin</c> e os <c>CHECK</c> são específicos do PostgreSQL.
/// </summary>
public sealed class QuotaRestoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A consulta devolve os Candidatos cujo bloqueio terminou, no limite exato e depois dele, e só esses.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListQuotaDueReturnsOnlyTheCandidatesWhoseBlockEnded()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new KatchWebApplicationFactory();
        Candidate atTheLimit = Build("limite@katch.pt", Candidate.MaxInterests, Now.AddHours(-24));
        Candidate longAfter = Build("antigo@katch.pt", Candidate.MaxInterests, Now.AddDays(-30));
        Candidate oneTickLeft = Build("quase@katch.pt", Candidate.MaxInterests, Now.AddHours(-24).AddTicks(1));
        Candidate stillBlocked = Build("bloqueado@katch.pt", Candidate.MaxInterests, Now.AddHours(-1));
        Candidate partial = Build("parcial@katch.pt", 4, Now.AddDays(-30));
        Candidate untouched = Build("novo@katch.pt", 0, Now);
        await SeedAsync(factory, atTheLimit, longAfter, oneTickLeft, stillBlocked, partial, untouched);

        using IServiceScope scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICandidateRepository>();

        List<Candidate> due = await repository.ListQuotaDueAsync(Now, cancellationToken);

        Assert.Equal(
            new[] { atTheLimit.Id, longAfter.Id }.Order(),
            due.Select(candidate => candidate.Id).Order());
    }

    /// <summary>
    /// O serviço repõe e grava a quota integral dos Candidatos vencidos e não toca nos outros; um tick depois repõe o
    /// que estava a um tick do fim.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ServiceRestoresAndPersistsOnlyTheDueQuotas()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new KatchWebApplicationFactory();
        factory.Clock.Fixed = Now;
        Candidate due = Build("vencido@katch.pt", Candidate.MaxInterests, Now.AddHours(-24));
        Candidate oneTickLeft = Build("quase@katch.pt", Candidate.MaxInterests, Now.AddHours(-24).AddTicks(1));
        Candidate partial = Build("parcial@katch.pt", 4, Now.AddDays(-30));
        await SeedAsync(factory, due, oneTickLeft, partial);

        Assert.Equal(1, await RestoreAsync(factory, cancellationToken));

        await AssertQuotaAsync(factory, due.Id, Candidate.MaxInterests, null, cancellationToken);
        await AssertQuotaAsync(factory, oneTickLeft.Id, 0, Now.AddTicks(1), cancellationToken);
        await AssertQuotaAsync(factory, partial.Id, 6, null, cancellationToken);

        Assert.Equal(0, await RestoreAsync(factory, cancellationToken));

        factory.Clock.Fixed = Now.AddTicks(1);
        Assert.Equal(1, await RestoreAsync(factory, cancellationToken));

        await AssertQuotaAsync(factory, oneTickLeft.Id, Candidate.MaxInterests, null, cancellationToken);
        await AssertQuotaAsync(factory, partial.Id, 6, null, cancellationToken);
    }

    /// <summary>
    /// A tarefa periódica, arrancada com a API, repõe a quota vencida sem que ninguém faça um pedido.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task HostedWorkerRestoresTheDueQuotaWithoutAnyRequest()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new KatchWebApplicationFactory { BackgroundJobsIntervalSeconds = 2 };
        factory.Clock.Fixed = Now;
        Candidate due = Build("vencido@katch.pt", Candidate.MaxInterests, Now.AddHours(-24));
        Candidate stillBlocked = Build("bloqueado@katch.pt", Candidate.MaxInterests, Now.AddHours(-23));
        await SeedAsync(factory, due, stillBlocked);

        Candidate? restored = null;
        DateTime giveUp = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < giveUp)
        {
            restored = await LoadAsync(factory, due.Id, cancellationToken);
            if (restored.SwipeRightsRemaining == Candidate.MaxInterests)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        Assert.NotNull(restored);
        Assert.Equal(Candidate.MaxInterests, restored.SwipeRightsRemaining);
        Assert.Null(restored.NextSwipeAvailableAt);
        await AssertQuotaAsync(factory, stillBlocked.Id, 0, Now.AddHours(1), cancellationToken);
    }

    /// <summary>
    /// A tarefa de reposição fica registada como serviço de fundo da API.
    /// </summary>
    [Fact]
    public void WorkerIsRegisteredAsAHostedService()
    {
        using var factory = new KatchWebApplicationFactory();

        Assert.Contains(factory.Services.GetServices<IHostedService>(), service => service is QuotaRestoreWorker);
    }

    /// <summary>
    /// Um intervalo inválido na configuração impede o arranque da API, com uma mensagem que diz como corrigir.
    /// </summary>
    [Fact]
    public void InvalidIntervalStopsTheApplicationAtStartup()
    {
        using var factory = new KatchWebApplicationFactory { BackgroundJobsIntervalSeconds = 0 };

        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => factory.Services);

        Assert.Contains("BackgroundJobs__IntervalSeconds", error.Message, StringComparison.Ordinal);
    }

    private static Candidate Build(string email, int interests, DateTimeOffset lastInterest)
    {
        var user = new AppUser(email, "hash", UserType.Candidate, Now.AddYears(-1), Now.AddYears(-1));
        var candidate = new Candidate(user, "Ana Silva", "912345678", Guid.NewGuid());
        for (int index = 0; index < interests; index++)
        {
            candidate.ConsumeInterest(lastInterest);
        }

        return candidate;
    }

    private static async Task SeedAsync(KatchWebApplicationFactory factory, params Candidate[] candidates)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        var location = new Location("Leiria", "Leiria", 39.743620m, -8.807050m);
        context.Locations.Add(location);
        foreach (Candidate candidate in candidates)
        {
            candidate.LocationId = location.Id;
            context.Candidates.Add(candidate);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<int> RestoreAsync(KatchWebApplicationFactory factory, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IQuotaService>().RestoreDueQuotasAsync(cancellationToken);
    }

    private static async Task<Candidate> LoadAsync(KatchWebApplicationFactory factory, Guid id, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Candidates.AsNoTracking().SingleAsync(candidate => candidate.Id == id, cancellationToken);
    }

    private static async Task AssertQuotaAsync(
        KatchWebApplicationFactory factory,
        Guid id,
        int remaining,
        DateTimeOffset? blockedUntil,
        CancellationToken cancellationToken)
    {
        Candidate saved = await LoadAsync(factory, id, cancellationToken);

        Assert.Equal(remaining, saved.SwipeRightsRemaining);
        Assert.Equal(blockedUntil, saved.NextSwipeAvailableAt);
    }
}
