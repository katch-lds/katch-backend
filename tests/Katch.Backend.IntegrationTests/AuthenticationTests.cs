// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes da autenticação JWT, da verificação do estado da conta e das políticas de autorização, sobre os
/// endpoints de <see cref="AuthorizationProbeController"/>.
/// </summary>
public sealed class AuthenticationTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string InvalidSession = "A sessão expirou ou não é válida. Inicie sessão novamente.";
    private const string AccountBlocked = "A conta está bloqueada.";
    private const string AccountSuspended = "A conta está suspensa.";
    private const string WrongAccountType = "A operação não está disponível para este tipo de conta.";

    private static readonly string[] Endpoints = ["any", "candidate", "recruiter", "candidate-or-recruiter", "admin"];

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public AuthenticationTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Gets the endpoints of the probe controller, for the tests that apply to all of them.
    /// </summary>
    public static TheoryData<string> AllEndpoints => new(Endpoints);

    /// <summary>
    /// Uma conta ativa do tipo certo passa a política.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="endpoint">Endpoint protegido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, "candidate")]
    [InlineData(UserType.Recruiter, "recruiter")]
    [InlineData(UserType.Candidate, "candidate-or-recruiter")]
    [InlineData(UserType.Recruiter, "candidate-or-recruiter")]
    [InlineData(UserType.Admin, "admin")]
    [InlineData(UserType.Candidate, "any")]
    [InlineData(UserType.Recruiter, "any")]
    [InlineData(UserType.Admin, "any")]
    public async Task ValidTokenOfTheRightTypeReturnsOk(UserType userType, string endpoint)
    {
        AppUser user = await this.SeedAsync(userType);

        using HttpResponseMessage response = await this.GetAsync(endpoint, this.IssueToken(user));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Sem credencial, o pedido é recusado com o ERR-01.
    /// </summary>
    /// <param name="endpoint">Endpoint protegido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task MissingTokenReturnsErr01(string endpoint)
    {
        using HttpResponseMessage response = await this.GetAsync(endpoint, token: null);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSession);
    }

    /// <summary>
    /// Uma credencial assinada com outro segredo é recusada com o ERR-01.
    /// </summary>
    /// <param name="endpoint">Endpoint protegido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task TokenWithInvalidSignatureReturnsErr01(string endpoint)
    {
        AppUser user = await this.SeedAsync(UserType.Admin);
        var otherService = new JwtTokenService(
            Options.Create(new JwtOptions { Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) }),
            this.factory.Clock);

        using HttpResponseMessage response = await this.GetAsync(endpoint, otherService.CreateToken(user).Token);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSession);
    }

    /// <summary>
    /// Uma credencial mal formada é recusada com o ERR-01.
    /// </summary>
    /// <param name="token">Texto enviado como credencial.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("isto-nao-e-um-jwt")]
    [InlineData("abc.def.ghi")]
    public async Task MalformedTokenReturnsErr01(string token)
    {
        using HttpResponseMessage response = await this.GetAsync("any", token);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSession);
    }

    /// <summary>
    /// Uma credencial expirada é recusada com o ERR-01, mesmo um segundo depois do <c>exp</c>
    /// (<c>ClockSkew</c> zero).
    /// </summary>
    /// <param name="secondsPastExpiry">Segundos decorridos desde o <c>exp</c>.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(1)]
    [InlineData(3600)]
    public async Task ExpiredTokenReturnsErr01(int secondsPastExpiry)
    {
        AppUser user = await this.SeedAsync(UserType.Candidate);
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow.AddHours(-8).AddSeconds(-secondsPastExpiry);

        using HttpResponseMessage response = await this.GetAsync("any", this.IssueToken(user, issuedAt));

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSession);
    }

    /// <summary>
    /// Uma credencial válida de uma conta que já não existe é recusada com o ERR-01.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TokenOfNonexistentAccountReturnsErr01()
    {
        var user = NewUser(UserType.Candidate);
        await this.EnsureDatabaseAsync();

        using HttpResponseMessage response = await this.GetAsync("any", this.IssueToken(user));

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSession);
    }

    /// <summary>
    /// Uma conta bloqueada é recusada com o ERR-03, antes da política (mesmo no endpoint de outro tipo de conta).
    /// </summary>
    /// <param name="endpoint">Endpoint protegido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("recruiter")]
    [InlineData("candidate")]
    [InlineData("any")]
    public async Task BlockedAccountReturnsErr03(string endpoint)
    {
        AppUser user = await this.SeedAsync(UserType.Recruiter, user => user.Block(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await this.GetAsync(endpoint, this.IssueToken(user));

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-03", AccountBlocked);
    }

    /// <summary>
    /// Uma conta suspensa é recusada com o ERR-04.
    /// </summary>
    /// <param name="endpoint">Endpoint protegido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("candidate")]
    [InlineData("recruiter")]
    [InlineData("any")]
    public async Task SuspendedAccountReturnsErr04(string endpoint)
    {
        AppUser user = await this.SeedAsync(UserType.Candidate, user => user.Suspend(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await this.GetAsync(endpoint, this.IssueToken(user));

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-04", AccountSuspended);
    }

    /// <summary>
    /// O bloqueio produz efeito no pedido seguinte, sem esperar pela expiração da credencial.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BlockingAfterIssuingTheTokenTakesEffectOnTheNextRequest()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AppUser user = await this.SeedAsync(UserType.Candidate);
        string token = this.IssueToken(user);

        using HttpResponseMessage before = await this.GetAsync("candidate", token);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            AppUser saved = await context.Users.SingleAsync(candidate => candidate.Id == user.Id, cancellationToken);
            saved.Block(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(cancellationToken);
        }

        using HttpResponseMessage after = await this.GetAsync("candidate", token);

        await AssertErrorAsync(after, HttpStatusCode.Forbidden, "ERR-03", AccountBlocked);
    }

    /// <summary>
    /// Uma conta ativa de outro tipo não cumpre a política e recebe o ERR-06.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="endpoint">Endpoint protegido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, "recruiter")]
    [InlineData(UserType.Candidate, "admin")]
    [InlineData(UserType.Recruiter, "candidate")]
    [InlineData(UserType.Recruiter, "admin")]
    [InlineData(UserType.Admin, "candidate")]
    [InlineData(UserType.Admin, "recruiter")]
    [InlineData(UserType.Admin, "candidate-or-recruiter")]
    public async Task WrongAccountTypeReturnsErr06(UserType userType, string endpoint)
    {
        AppUser user = await this.SeedAsync(userType);

        using HttpResponseMessage response = await this.GetAsync(endpoint, this.IssueToken(user));

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-06", WrongAccountType);
    }

    /// <summary>
    /// Num endpoint anónimo, uma credencial de conta bloqueada, inexistente ou expirada não é verificada: responde 200,
    /// enquanto o endpoint protegido continua a recusar com o erro respetivo.
    /// </summary>
    /// <param name="scenario">Estado da conta ou da credencial.</param>
    /// <param name="expectedStatus">Estado HTTP esperado no endpoint protegido.</param>
    /// <param name="expectedCode">Código de erro esperado no endpoint protegido.</param>
    /// <param name="expectedMessage">Mensagem esperada no endpoint protegido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("blocked", HttpStatusCode.Forbidden, "ERR-03", AccountBlocked)]
    [InlineData("nonexistent", HttpStatusCode.Unauthorized, "ERR-01", InvalidSession)]
    [InlineData("expired", HttpStatusCode.Unauthorized, "ERR-01", InvalidSession)]
    public async Task AnonymousEndpointIgnoresTheAccountStateButProtectedOneDoesNot(
        string scenario,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedMessage)
    {
        string token;
        switch (scenario)
        {
            case "blocked":
                token = this.IssueToken(await this.SeedAsync(UserType.Recruiter, user => user.Block(DateTimeOffset.UtcNow)));
                break;
            case "nonexistent":
                await this.EnsureDatabaseAsync();
                token = this.IssueToken(NewUser(UserType.Candidate));
                break;
            default:
                token = this.IssueToken(
                    await this.SeedAsync(UserType.Candidate),
                    DateTimeOffset.UtcNow.AddHours(-9));
                break;
        }

        using HttpResponseMessage anonymous = await this.GetAsync("anonymous", token);
        using HttpResponseMessage protectedResponse = await this.GetAsync("any", token);

        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        await AssertErrorAsync(protectedResponse, expectedStatus, expectedCode, expectedMessage);
    }

    private static AppUser NewUser(UserType userType)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", userType, now, now);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        string message)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement root = json.RootElement;

        Assert.Equal(
            ["blockedUntil", "code", "fields", "message"],
            root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal(message, root.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("fields").ValueKind);
        Assert.Equal(0, root.GetProperty("fields").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("blockedUntil").ValueKind);
    }

    private async Task EnsureDatabaseAsync()
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    private async Task<AppUser> SeedAsync(UserType userType, Action<AppUser>? configure = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AppUser user = NewUser(userType);
        configure?.Invoke(user);

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    private string IssueToken(AppUser user, DateTimeOffset? issuedAt = null)
    {
        ITokenService tokens = this.factory.Services.GetRequiredService<ITokenService>();
        this.factory.Clock.Fixed = issuedAt;
        try
        {
            return tokens.CreateToken(user).Token;
        }
        finally
        {
            this.factory.Clock.Fixed = null;
        }
    }

    private async Task<HttpResponseMessage> GetAsync(string endpoint, string? token)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/test-authorization/{endpoint}", UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
