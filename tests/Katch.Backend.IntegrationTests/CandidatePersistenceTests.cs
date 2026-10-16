// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de persistência de <see cref="Candidate"/>, <see cref="Location"/> e <see cref="CandidateLink"/> sobre
/// SQLite in-memory. Os <c>CHECK</c>, os tipos enumerados e o <c>xmin</c> são específicos do PostgreSQL e estão
/// verificados nos testes unitários do mapeamento.
/// </summary>
public sealed class CandidatePersistenceTests : IClassFixture<KatchWebApplicationFactory>
{
    private static readonly DateTimeOffset Registered = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidatePersistenceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public CandidatePersistenceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// O Candidato é gravado com a localidade, as preferências, a quota e as hiperligações, e lido de volta igual.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CandidateIsSavedAndReadBackWithPreferencesQuotaAndLinks()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var location = new Location("Leiria", "Leiria", 39.743620m, -8.807050m);
        var user = new AppUser("ana@katch.pt", "hash", UserType.Candidate, Registered, Registered);
        var candidate = new Candidate(user, "Ana Silva", "912345678", location.Id)
        {
            DesiredRole = "Programadora",
            ExperienceSummary = "Cinco anos de experiência.",
            Availability = Availability.FifteenDays,
            MaxDistanceKm = 50,
            MinSalaryExpectation = 1500.50m,
            PreferredWorkModes = [WorkMode.Remote, WorkMode.Hybrid],
            PreferredContractTypes = [ContractType.Permanent],
        };
        candidate.SetLinks(["https://github.com/ana", "https://linkedin.com/in/ana"]);
        for (int index = 0; index < Candidate.MaxInterests; index++)
        {
            candidate.ConsumeInterest(Registered);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            context.Locations.Add(location);
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Candidate saved = await context.Candidates
                .Include(item => item.User)
                .Include(item => item.Links)
                .SingleAsync(item => item.Id == user.Id, cancellationToken);

            Assert.Equal("ana@katch.pt", saved.User.Email);
            Assert.Equal("Ana Silva", saved.FullName);
            Assert.Equal("912345678", saved.PhoneNumber);
            Assert.Equal(location.Id, saved.LocationId);
            Assert.Equal("Programadora", saved.DesiredRole);
            Assert.Equal(Availability.FifteenDays, saved.Availability);
            Assert.Equal(50, saved.MaxDistanceKm);
            Assert.Equal(1500.50m, saved.MinSalaryExpectation);
            Assert.Equal([WorkMode.Remote, WorkMode.Hybrid], saved.PreferredWorkModes);
            Assert.Equal([ContractType.Permanent], saved.PreferredContractTypes);
            Assert.Equal(0, saved.SwipeRightsRemaining);
            Assert.Equal(Registered.AddHours(24), saved.NextSwipeAvailableAt);
            Assert.Equal(
                ["https://github.com/ana", "https://linkedin.com/in/ana"],
                saved.Links.OrderBy(link => link.Position).Select(link => link.Url).ToArray());
        }
    }

    /// <summary>
    /// Substituir as hiperligações grava as atualizadas e elimina as que sobram, sem violar as posições únicas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReplacingLinksUpdatesAndDeletesTheRemainingOnes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var location = new Location("Porto", "Porto", 41.157944m, -8.629105m);
        var user = new AppUser("rui@katch.pt", "hash", UserType.Candidate, Registered, Registered);
        var candidate = new Candidate(user, "Rui Costa", "923456789", location.Id);
        candidate.SetLinks(["https://a.pt", "https://b.pt", "https://c.pt"]);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            context.Locations.Add(location);
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync(cancellationToken);

            candidate.SetLinks(["https://x.pt"]);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            CandidateLink[] links = await context.CandidateLinks
                .Where(link => link.CandidateId == user.Id)
                .ToArrayAsync(cancellationToken);

            CandidateLink only = Assert.Single(links);
            Assert.Equal("https://x.pt", only.Url);
            Assert.Equal(1, only.Position);
        }
    }

    /// <summary>
    /// Um Candidato com uma localidade inexistente é rejeitado pela chave estrangeira.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CandidateWithUnknownLocationIsRejectedByTheForeignKey()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var user = new AppUser("sem-local@katch.pt", "hash", UserType.Candidate, Registered, Registered);

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        context.Candidates.Add(new Candidate(user, "Sem Local", "934567890", Guid.NewGuid()));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
    }
}
