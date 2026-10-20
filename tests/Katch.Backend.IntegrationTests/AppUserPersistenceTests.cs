// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de persistência de <see cref="AppUser"/> sobre SQLite in-memory. Os <c>CHECK</c> e o índice único sobre
/// <c>lower(email)</c> são específicos do PostgreSQL e estão verificados nos testes unitários do mapeamento e
/// na evidência da migration.
/// </summary>
public sealed class AppUserPersistenceTests : IClassFixture<KatchWebApplicationFactory>
{
    private static readonly DateTimeOffset Registered = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUserPersistenceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public AppUserPersistenceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Uma conta gravada é lida com os mesmos valores, incluindo o tipo, o estado e as datas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SavedAccountIsReadBackWithTheSameValues()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var user = new AppUser("persistencia@katch.pt", "hash", UserType.Recruiter, Registered, Registered);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            AppUser saved = await context.Users.SingleAsync(candidate => candidate.Id == user.Id, cancellationToken);

            Assert.Equal("persistencia@katch.pt", saved.Email);
            Assert.Equal("hash", saved.PasswordHash);
            Assert.Equal(UserType.Recruiter, saved.UserType);
            Assert.Equal(AccountStatus.Active, saved.Status);
            Assert.Equal(Registered, saved.TermsAcceptedAt);
            Assert.Equal(Registered, saved.CreatedAt);
            Assert.Equal(Registered, saved.UpdatedAt);
        }
    }

    /// <summary>
    /// Uma mudança de estado é gravada e lida de volta.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task StatusChangeIsPersisted()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var user = new AppUser("estado@katch.pt", "hash", UserType.Candidate, Registered, Registered);
        DateTimeOffset blockedAt = Registered.AddHours(2);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);

            user.Block(blockedAt);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            AppUser saved = await context.Users.SingleAsync(candidate => candidate.Id == user.Id, cancellationToken);

            Assert.Equal(AccountStatus.Blocked, saved.Status);
            Assert.Equal(blockedAt, saved.UpdatedAt);
        }
    }
}
