// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de <c>PUT /api/auth/password</c> sobre a API em memória (documentação da API v02, secção 6.1.5; RF002, RF040,
/// RF084).
/// </summary>
public sealed class ChangePasswordTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/auth/password";
    private const string NewPassword = "nova2026";
    private const string PasswordPolicy = "A palavra-passe tem de ter pelo menos 8 caracteres, uma letra e um algarismo.";
    private const string InvalidCredentials = "O endereço de correio eletrónico ou a palavra-passe estão incorretos.";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangePasswordTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public ChangePasswordTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Com a palavra-passe atual correta e uma nova válida, responde 204 e o início de sessão seguinte só aceita a nova
    /// (critério de aceitação do RF002, RF040 e RF084), para cada tipo de conta.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="clientApp">Ponto de acesso do tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, "mobile")]
    [InlineData(UserType.Recruiter, "web")]
    [InlineData(UserType.Admin, "web")]
    public async Task ValidChangeReturns204AndOnlyTheNewPasswordSignsIn(UserType userType, string clientApp)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, userType);

        using HttpResponseMessage response = await this.PutAsync(
            this.IssueToken(user),
            new { currentPassword = AuthTestAccounts.Password, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        using HttpResponseMessage signInWithOld = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            clientApp);
        using HttpResponseMessage signInWithNew = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, NewPassword),
            clientApp);
        await AuthenticationTests.AssertErrorAsync(signInWithOld, HttpStatusCode.Unauthorized, "ERR-02", InvalidCredentials);
        Assert.Equal(HttpStatusCode.OK, signInWithNew.StatusCode);
    }

    /// <summary>
    /// Só o resumo da nova palavra-passe é gravado, com valor aleatório próprio, e a data da alteração é registada
    /// (RNF003).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OnlyTheHashOfTheNewPasswordIsStored()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        string previousHash = user.PasswordHash;
        var changedAt = new DateTimeOffset(2026, 10, 24, 11, 0, 0, TimeSpan.Zero);
        string token = this.IssueToken(user);
        this.factory.Clock.Fixed = changedAt;
        try
        {
            using HttpResponseMessage response = await this.PutAsync(
                token,
                new { currentPassword = AuthTestAccounts.Password, newPassword = NewPassword });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        finally
        {
            this.factory.Clock.Fixed = null;
        }

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        AppUser saved = await context.Users.AsNoTracking().SingleAsync(account => account.Id == user.Id, cancellationToken);
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        Assert.NotEqual(previousHash, saved.PasswordHash);
        Assert.NotEqual(NewPassword, saved.PasswordHash);
        Assert.DoesNotContain(NewPassword, saved.PasswordHash, StringComparison.Ordinal);
        Assert.True(hasher.Verify(saved.PasswordHash, NewPassword));
        Assert.False(hasher.Verify(saved.PasswordHash, AuthTestAccounts.Password));
        Assert.Equal(changedAt, saved.UpdatedAt);
    }

    /// <summary>
    /// A credencial usada na alteração mantém-se válida até expirar (documentação da API v02, secção 6.1.5).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CurrentTokenRemainsValidAfterTheChange()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        string token = this.IssueToken(user);

        using HttpResponseMessage change = await this.PutAsync(
            token,
            new { currentPassword = AuthTestAccounts.Password, newPassword = NewPassword });
        using HttpResponseMessage next = await this.PutAsync(
            token,
            new { currentPassword = NewPassword, newPassword = "outra2026" });

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, next.StatusCode);
    }

    /// <summary>
    /// A palavra-passe atual errada é recusada com 400 ERR-10 (e não 401) no campo <c>currentPassword</c>, e nada é
    /// alterado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WrongCurrentPasswordReturns400AndChangesNothing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Recruiter);

        using HttpResponseMessage response = await this.PutAsync(
            this.IssueToken(user),
            new { currentPassword = "errada2026", newPassword = NewPassword });

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal([("currentPassword", "A palavra-passe atual está errada.")], AuthTestAccounts.Fields(error));
        using HttpResponseMessage stillWorks = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            "web");
        Assert.Equal(HttpStatusCode.OK, stillWorks.StatusCode);
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        AppUser saved = await context.Users.AsNoTracking().SingleAsync(account => account.Id == user.Id, cancellationToken);
        Assert.Equal(user.PasswordHash, saved.PasswordHash);
    }

    /// <summary>
    /// Uma palavra-passe nova fora do P05 é recusada com 400 ERR-10 no campo <c>newPassword</c>; «katch» é o exemplo do
    /// critério de aceitação.
    /// </summary>
    /// <param name="newPassword">Palavra-passe nova enviada.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("katch", PasswordPolicy)]
    [InlineData("katch20", PasswordPolicy)]
    [InlineData("katchkatch", PasswordPolicy)]
    [InlineData("2026202620", PasswordPolicy)]
    [InlineData("", "O campo é obrigatório.")]
    [InlineData("        ", "O campo é obrigatório.")]
    public async Task NewPasswordOutsideP05Returns400(string newPassword, string reason)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage response = await this.PutAsync(
            this.IssueToken(user),
            new { currentPassword = AuthTestAccounts.Password, newPassword });

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal([("newPassword", reason)], AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// Uma palavra-passe nova com mais de 128 caracteres é recusada.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NewPasswordOver128CharactersReturns400()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage response = await this.PutAsync(
            this.IssueToken(user),
            new { currentPassword = AuthTestAccounts.Password, newPassword = new string('a', 128) + "1" });

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal([("newPassword", "A palavra-passe tem no máximo 128 caracteres.")], AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// Com a atual errada e a nova inválida, os dois campos são indicados na mesma resposta.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BothFieldsAreReportedInTheSameResponse()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage response = await this.PutAsync(
            this.IssueToken(user),
            new { currentPassword = "errada2026", newPassword = "katch" });

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal(
            [("currentPassword", "A palavra-passe atual está errada."), ("newPassword", PasswordPolicy)],
            AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// Sem corpo, ou com os campos vazios, os dois campos são indicados como obrigatórios.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmptyBodyReportsBothFieldsAsRequired()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(Route, UriKind.Relative))
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.IssueToken(user));

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal(
            [("currentPassword", "O campo é obrigatório."), ("newPassword", "O campo é obrigatório.")],
            AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// A conta alterada é sempre a da credencial: a credencial de uma conta nunca altera a palavra-passe de outra.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OnlyTheAccountOfTheTokenIsChanged()
    {
        AppUser owner = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        AppUser other = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage response = await this.PutAsync(
            this.IssueToken(owner),
            new
            {
                userId = other.Id,
                email = other.Email,
                currentPassword = AuthTestAccounts.Password,
                newPassword = NewPassword,
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using HttpResponseMessage otherStillWorks = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(other.Email, AuthTestAccounts.Password),
            "mobile");
        using HttpResponseMessage ownerChanged = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(owner.Email, NewPassword),
            "mobile");
        Assert.Equal(HttpStatusCode.OK, otherStillWorks.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerChanged.StatusCode);
    }

    /// <summary>
    /// Sem credencial, ou com uma credencial inválida, o pedido é recusado com o ERR-01 e nada é alterado.
    /// </summary>
    /// <param name="token">Credencial enviada, ou <see langword="null"/> se não houver.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("abc.def.ghi")]
    public async Task MissingOrInvalidTokenReturnsErr01(string? token)
    {
        using HttpResponseMessage response = await this.PutAsync(
            token,
            new { currentPassword = AuthTestAccounts.Password, newPassword = NewPassword });

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            "ERR-01",
            "A sessão expirou ou não é válida. Inicie sessão novamente.");
    }

    /// <summary>
    /// Uma conta bloqueada ou suspensa não pode alterar a palavra-passe, mesmo com a credencial ainda válida.
    /// </summary>
    /// <param name="suspended"><see langword="true"/> para suspender a conta; <see langword="false"/> para a bloquear.</param>
    /// <param name="code">Código esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(false, "ERR-03")]
    [InlineData(true, "ERR-04")]
    public async Task BlockedOrSuspendedAccountReturns403AndChangesNothing(bool suspended, string code)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AppUser user = await AuthTestAccounts.SeedAsync(
            this.factory,
            UserType.Candidate,
            account =>
            {
                if (suspended)
                {
                    account.Suspend(DateTimeOffset.UtcNow);
                }
                else
                {
                    account.Block(DateTimeOffset.UtcNow);
                }
            });

        using HttpResponseMessage response = await this.PutAsync(
            this.IssueToken(user),
            new { currentPassword = AuthTestAccounts.Password, newPassword = NewPassword });

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Forbidden,
            code,
            suspended ? "A conta está suspensa." : "A conta está bloqueada.");
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        AppUser saved = await context.Users.AsNoTracking().SingleAsync(account => account.Id == user.Id, cancellationToken);
        Assert.Equal(user.PasswordHash, saved.PasswordHash);
    }

    /// <summary>
    /// Um corpo que não seja JSON é recusado com 415.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NonJsonContentTypeReturns415()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(Route, UriKind.Relative))
        {
            Content = new StringContent("currentPassword=a&newPassword=b", Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.IssueToken(user));

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    private static async Task<JsonElement> AssertErr10Async(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-10", root.GetProperty("code").GetString());
        Assert.Equal("Os dados enviados não cumprem a validação automática.", root.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("blockedUntil").ValueKind);
        return root;
    }

    private string IssueToken(AppUser user)
    {
        return this.factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
    }

    private async Task<HttpResponseMessage> PutAsync(string? token, object body)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(Route, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
