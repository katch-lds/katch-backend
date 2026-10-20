// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de persistência de <see cref="Recruiter"/> sobre SQLite in-memory.
/// </summary>
public sealed class RecruiterPersistenceTests : IClassFixture<KatchWebApplicationFactory>
{
    private static readonly DateTimeOffset Registered = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecruiterPersistenceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public RecruiterPersistenceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// O Recrutador é gravado com a conta de utilizador e lido de volta com o mesmo identificador.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RecruiterIsSavedWithItsAccountAndSharesTheKey()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var user = new AppUser("recrutador@katch.pt", "hash", UserType.Recruiter, Registered, Registered);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            context.Recruiters.Add(new Recruiter(user));
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Recruiter saved = await context.Recruiters
                .Include(recruiter => recruiter.User)
                .SingleAsync(recruiter => recruiter.Id == user.Id, cancellationToken);

            Assert.Equal(user.Id, saved.User.Id);
            Assert.Equal("recrutador@katch.pt", saved.User.Email);
            Assert.Equal(UserType.Recruiter, saved.User.UserType);
        }
    }

    /// <summary>
    /// Um Recrutador sem conta de utilizador correspondente é rejeitado pela chave estrangeira.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RecruiterWithoutAccountIsRejectedByTheForeignKey()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var user = new AppUser("orfao@katch.pt", "hash", UserType.Recruiter, Registered, Registered);

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        context.Recruiters.Add(new Recruiter(user));
        context.Entry(user).State = EntityState.Unchanged;

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
    }
}
