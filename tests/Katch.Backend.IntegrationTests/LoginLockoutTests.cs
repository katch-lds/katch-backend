// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes do bloqueio temporário do início de sessão por tentativas falhadas (SEG-002): 5 falhas seguidas bloqueiam a
/// conta 1 minuto, cada novo bloqueio dobra a espera, e durante o bloqueio a resposta é sempre o ERR-02. Cada teste usa
/// uma aplicação própria, porque controla o relógio.
/// </summary>
public sealed class LoginLockoutTests : IDisposable
{
    private const string InvalidCredentials = "O endereço de correio eletrónico ou a palavra-passe estão incorretos.";
    private const string WrongPassword = "errada2026";

    private readonly KatchWebApplicationFactory factory = new();
    private readonly DateTimeOffset start = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public void Dispose()
    {
        this.factory.Dispose();
    }

    /// <summary>
    /// Depois de 5 falhas seguidas, a palavra-passe certa recebe o mesmo ERR-02 até passar 1 minuto; depois, entra.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task FiveFailuresLockTheAccountForOneMinute()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        this.factory.Clock.Fixed = this.start;
        await this.FailAsync(user, 5);

        using HttpResponseMessage locked = await this.LoginAsync(user, AuthTestAccounts.Password);
        this.factory.Clock.Fixed = this.start.AddSeconds(59);
        using HttpResponseMessage stillLocked = await this.LoginAsync(user, AuthTestAccounts.Password);
        this.factory.Clock.Fixed = this.start.AddSeconds(61);
        using HttpResponseMessage unlocked = await this.LoginAsync(user, AuthTestAccounts.Password);

        await AuthenticationTests.AssertErrorAsync(locked, HttpStatusCode.Unauthorized, "ERR-02", InvalidCredentials);
        await AuthenticationTests.AssertErrorAsync(stillLocked, HttpStatusCode.Unauthorized, "ERR-02", InvalidCredentials);
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
    }

    /// <summary>
    /// Quatro falhas não bloqueiam, e um início de sessão válido repõe o contador.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ValidLoginResetsTheCounter()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        this.factory.Clock.Fixed = this.start;

        await this.FailAsync(user, 4);
        using HttpResponseMessage valid = await this.LoginAsync(user, AuthTestAccounts.Password);
        await this.FailAsync(user, 4);
        using HttpResponseMessage stillValid = await this.LoginAsync(user, AuthTestAccounts.Password);

        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
        AppUser saved = await this.ReadAsync(user.Id);
        Assert.Equal(0, saved.FailedLoginCount);
        Assert.Null(saved.LockedUntil);
    }

    /// <summary>
    /// Uma nova série de 5 falhas sem início de sessão válido dobra o bloqueio (2 minutos); as tentativas durante o
    /// bloqueio não contam.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EachNewLockDoublesTheWait()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        this.factory.Clock.Fixed = this.start;
        await this.FailAsync(user, 5);
        await this.FailAsync(user, 3);

        this.factory.Clock.Fixed = this.start.AddSeconds(61);
        await this.FailAsync(user, 5);
        this.factory.Clock.Fixed = this.start.AddSeconds(61 + 119);
        using HttpResponseMessage locked = await this.LoginAsync(user, AuthTestAccounts.Password);
        this.factory.Clock.Fixed = this.start.AddSeconds(61 + 121);
        using HttpResponseMessage unlocked = await this.LoginAsync(user, AuthTestAccounts.Password);

        await AuthenticationTests.AssertErrorAsync(locked, HttpStatusCode.Unauthorized, "ERR-02", InvalidCredentials);
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
    }

    private async Task FailAsync(AppUser user, int count)
    {
        for (int i = 0; i < count; i++)
        {
            using HttpResponseMessage response = await this.LoginAsync(user, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private Task<HttpResponseMessage> LoginAsync(AppUser user, string password)
    {
        return AuthTestAccounts.LoginAsync(this.factory, AuthTestAccounts.Credentials(user.Email, password), "mobile");
    }

    private async Task<AppUser> ReadAsync(Guid userId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<KatchDbContext>().Users
            .AsNoTracking()
            .SingleAsync(account => account.Id == userId, TestContext.Current.CancellationToken);
    }
}
