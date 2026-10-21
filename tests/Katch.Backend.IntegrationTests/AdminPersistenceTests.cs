// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de persistência de <see cref="Admin"/> sobre SQLite in-memory.
/// </summary>
public sealed class AdminPersistenceTests : IClassFixture<KatchWebApplicationFactory>
{
    private static readonly DateTimeOffset Registered = new(2026, 10, 22, 18, 0, 0, TimeSpan.Zero);

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminPersistenceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public AdminPersistenceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// O Administrador é gravado com a conta de utilizador e lido de volta com o mesmo identificador.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AdminIsSavedWithItsAccountAndSharesTheKey()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var user = new AppUser("administrador@katch.pt", "hash", UserType.Admin, null, Registered);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            context.Admins.Add(new Admin(user));
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Admin saved = await context.Admins
                .Include(admin => admin.User)
                .SingleAsync(admin => admin.Id == user.Id, cancellationToken);

            Assert.Equal(user.Id, saved.User.Id);
            Assert.Equal("administrador@katch.pt", saved.User.Email);
            Assert.Equal(UserType.Admin, saved.User.UserType);
            Assert.Equal(AccountStatus.Active, saved.User.Status);
        }
    }

    /// <summary>
    /// Um Administrador sem conta de utilizador correspondente é rejeitado pela chave estrangeira.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AdminWithoutAccountIsRejectedByTheForeignKey()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var user = new AppUser("orfao-admin@katch.pt", "hash", UserType.Admin, null, Registered);

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        context.Admins.Add(new Admin(user));
        context.Entry(user).State = EntityState.Unchanged;

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
    }
}
