// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de <c>POST /api/auth/login</c> sobre a API em memória (documentação da API v02, secções 3.2 e 6.1.3).
/// </summary>
public sealed class LoginTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string InvalidCredentials = "O endereço de correio eletrónico ou a palavra-passe estão incorretos.";
    private const string AccountBlocked = "A conta está bloqueada.";
    private const string AccountSuspended = "A conta está suspensa.";
    private const string WrongAccessPoint = "Esta conta não tem acesso por este ponto de acesso.";
    private const string WrongPassword = "outra2026";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public LoginTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Com credenciais corretas, conta ativa e o ponto de acesso adequado, responde 200 com o <c>LoginResponse</c> e uma
    /// credencial aceite pela API na política do tipo de conta.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="clientApp">Ponto de acesso adequado.</param>
    /// <param name="probe">Endpoint de teste protegido pela política do tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, "mobile", "candidate")]
    [InlineData(UserType.Recruiter, "web", "recruiter")]
    [InlineData(UserType.Admin, "web", "admin")]
    public async Task ValidCredentialsReturn200WithSessionForEachAccountType(UserType userType, string clientApp, string probe)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, userType);

        using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            clientApp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(
            ["expiresAt", "token", "userId", "userType"],
            root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(userType.ToString().ToUpperInvariant(), root.GetProperty("userType").GetString());
        Assert.Equal(user.Id, root.GetProperty("userId").GetGuid());
        Assert.EndsWith("Z", root.GetProperty("expiresAt").GetString(), StringComparison.Ordinal);

        using HttpClient client = this.factory.CreateClient();
        using var next = new HttpRequestMessage(HttpMethod.Get, new Uri($"/test-authorization/{probe}", UriKind.Relative));
        next.Headers.Authorization = new AuthenticationHeaderValue("Bearer", root.GetProperty("token").GetString());
        using HttpResponseMessage authenticated = await client.SendAsync(next, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
    }

    /// <summary>
    /// A credencial expira 8 horas depois do início de sessão (RNF005, P20).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ExpiresAtIsEightHoursAfterTheSignIn()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        var signedInAt = new DateTimeOffset(2026, 10, 24, 9, 30, 0, TimeSpan.Zero);
        this.factory.Clock.Fixed = signedInAt;
        try
        {
            using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
                this.factory,
                AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
                "mobile");

            JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
            Assert.Equal("2026-10-24T17:30:00Z", root.GetProperty("expiresAt").GetString());
        }
        finally
        {
            this.factory.Clock.Fixed = null;
        }
    }

    /// <summary>
    /// O correio é comparado sem distinção de maiúsculas e sem os espaços do início e do fim; a palavra-passe distingue
    /// maiúsculas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmailIsCaseInsensitiveAndTrimmedButPasswordIsExact()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage accepted = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials($"  {user.Email.ToUpperInvariant()} ", AuthTestAccounts.Password),
            "mobile");
        using HttpResponseMessage otherCase = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password.ToUpperInvariant()),
            "mobile");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await AuthenticationTests.AssertErrorAsync(otherCase, HttpStatusCode.Unauthorized, "ERR-02", InvalidCredentials);
    }

    /// <summary>
    /// Correio inexistente e palavra-passe errada recebem exatamente a mesma resposta: 401 ERR-02 (RNF009).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownEmailAndWrongPasswordReturnTheSameErr02()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage unknown = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials($"inexistente-{Guid.NewGuid():N}@exemplo.pt", AuthTestAccounts.Password),
            "mobile");
        using HttpResponseMessage wrongPassword = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, WrongPassword),
            "mobile");

        string unknownBody = await unknown.Content.ReadAsStringAsync(cancellationToken);
        string wrongPasswordBody = await wrongPassword.Content.ReadAsStringAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(unknownBody, wrongPasswordBody);
        Assert.Equal(unknown.Content.Headers.ContentType, wrongPassword.Content.Headers.ContentType);
        using JsonDocument json = JsonDocument.Parse(unknownBody);
        Assert.Equal("ERR-02", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(InvalidCredentials, json.RootElement.GetProperty("message").GetString());
    }

    /// <summary>
    /// Uma conta bloqueada ou suspensa recebe o ERR-03 ou o ERR-04 só com a palavra-passe correta; com a errada recebe o
    /// ERR-02, para que o estado não seja revelado a quem não conhece a palavra-passe.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="clientApp">Ponto de acesso adequado.</param>
    /// <param name="suspended"><see langword="true"/> para suspender a conta; <see langword="false"/> para a bloquear.</param>
    /// <param name="code">Código esperado com a palavra-passe correta.</param>
    /// <param name="message">Mensagem esperada com a palavra-passe correta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, "mobile", false, "ERR-03", AccountBlocked)]
    [InlineData(UserType.Recruiter, "web", false, "ERR-03", AccountBlocked)]
    [InlineData(UserType.Candidate, "mobile", true, "ERR-04", AccountSuspended)]
    public async Task BlockedOrSuspendedAccountIsOnlyRevealedWithTheCorrectPassword(
        UserType userType,
        string clientApp,
        bool suspended,
        string code,
        string message)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(
            this.factory,
            userType,
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

        using HttpResponseMessage correct = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            clientApp);
        using HttpResponseMessage wrong = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, WrongPassword),
            clientApp);

        await AuthenticationTests.AssertErrorAsync(correct, HttpStatusCode.Forbidden, code, message);
        await AuthenticationTests.AssertErrorAsync(wrong, HttpStatusCode.Unauthorized, "ERR-02", InvalidCredentials);
    }

    /// <summary>
    /// Depois de verificadas as credenciais e o estado, o tipo de conta que entra pelo ponto de acesso errado recebe o
    /// ERR-05 (403).
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="clientApp">Ponto de acesso errado para o tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, "web")]
    [InlineData(UserType.Recruiter, "mobile")]
    [InlineData(UserType.Admin, "mobile")]
    public async Task WrongAccessPointReturnsErr05OnlyAfterTheCredentials(UserType userType, string clientApp)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, userType);

        using HttpResponseMessage correct = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            clientApp);
        using HttpResponseMessage wrong = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, WrongPassword),
            clientApp);

        await AuthenticationTests.AssertErrorAsync(correct, HttpStatusCode.Forbidden, "ERR-05", WrongAccessPoint);
        await AuthenticationTests.AssertErrorAsync(wrong, HttpStatusCode.Unauthorized, "ERR-02", InvalidCredentials);
    }

    /// <summary>
    /// O estado da conta vem antes do ponto de acesso: uma conta bloqueada que entra pelo ponto errado recebe o ERR-03.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AccountStateIsCheckedBeforeTheAccessPoint()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(
            this.factory,
            UserType.Candidate,
            account => account.Block(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            "web");

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-03", AccountBlocked);
    }

    /// <summary>
    /// Uma Empresa pendente, recusada ou suspensa, ou a falta de Empresa, não impede o início de sessão do Recrutador
    /// (RF038): só vedam as operações <c>ApprovedCompany</c>.
    /// </summary>
    /// <param name="companyStatus">Estado da Empresa, ou <see langword="null"/> se o Recrutador não tem Empresa.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData(CompanyStatus.Pending)]
    [InlineData(CompanyStatus.Rejected)]
    [InlineData(CompanyStatus.Suspended)]
    [InlineData(CompanyStatus.Approved)]
    public async Task RecruiterCanSignInWhateverTheCompanyState(CompanyStatus? companyStatus)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Recruiter, companyStatus: companyStatus);

        using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            "web");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("RECRUITER", root.GetProperty("userType").GetString());
    }

    /// <summary>
    /// Um cabeçalho <c>X-Client-App</c> em falta é recusado com 400 ERR-10, antes de qualquer verificação da conta.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MissingClientAppHeaderReturns400()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            clientApp: null);

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal([("X-Client-App", "O campo é obrigatório.")], AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// Um cabeçalho <c>X-Client-App</c> com outro valor que não seja exatamente <c>mobile</c> ou <c>web</c> é recusado com
    /// 400 ERR-10, mesmo com credenciais corretas.
    /// </summary>
    /// <param name="value">Valor enviado.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("desktop", "O valor não é válido.")]
    [InlineData("Mobile", "O valor não é válido.")]
    [InlineData("WEB", "O valor não é válido.")]
    [InlineData("mobile,web", "O valor não é válido.")]
    [InlineData("mobile web", "O valor não é válido.")]
    [InlineData("", "O campo é obrigatório.")]
    [InlineData("   ", "O campo é obrigatório.")]
    public async Task InvalidClientAppHeaderReturns400(string value, string reason)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            value);

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal([("X-Client-App", reason)], AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// Um cabeçalho repetido com valores diferentes não é aceite.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RepeatedClientAppHeaderReturns400()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(AuthTestAccounts.LoginRoute, UriKind.Relative))
        {
            Content = System.Net.Http.Json.JsonContent.Create(
                AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password)),
        };
        request.Headers.TryAddWithoutValidation("X-Client-App", ["mobile", "web"]);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertErr10Async(response);
    }

    /// <summary>
    /// Correio ou palavra-passe vazios, ou sem corpo, são indicados no mesmo ERR-10; só se verifica que não estão vazios.
    /// </summary>
    /// <param name="email">Correio enviado, ou <see langword="null"/> para o omitir.</param>
    /// <param name="password">Palavra-passe enviada, ou <see langword="null"/> para a omitir.</param>
    /// <param name="expectedFields">Campos esperados em erro.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null, null, "email,password")]
    [InlineData("", "", "email,password")]
    [InlineData("   ", "   ", "email,password")]
    [InlineData("ana@exemplo.pt", "", "password")]
    [InlineData("", "katch2026", "email")]
    public async Task EmptyFieldsReturn400WithEveryEmptyField(string? email, string? password, string expectedFields)
    {
        using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
            this.factory,
            new { email, password },
            "mobile");

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal(
            expectedFields.Split(','),
            AuthTestAccounts.Fields(error).Select(field => field.Field));
        Assert.All(AuthTestAccounts.Fields(error), field => Assert.Equal("O campo é obrigatório.", field.Reason));
    }

    /// <summary>
    /// Sem corpo, os dois campos são indicados como obrigatórios.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmptyBodyReportsBothFieldsAsRequired()
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(AuthTestAccounts.LoginRoute, UriKind.Relative))
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-Client-App", "mobile");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal(["email", "password"], AuthTestAccounts.Fields(error).Select(field => field.Field));
    }

    /// <summary>
    /// O formato do correio e a política da palavra-passe não são revelados: valores fora do formato recebem o ERR-02.
    /// </summary>
    /// <param name="email">Correio enviado.</param>
    /// <param name="password">Palavra-passe enviada.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("isto-nao-e-um-correio", "x")]
    [InlineData("ana@@exemplo.pt", "katch")]
    public async Task EmailFormatAndPasswordPolicyAreNotRevealed(string email, string password)
    {
        using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(email, password),
            "mobile");

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-02", InvalidCredentials);
    }

    /// <summary>
    /// Valores muito longos não rebentam o pedido: são recusados como credenciais erradas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task VeryLongValuesAreRejectedAsWrongCredentials()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage longPassword = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, new string('a', 100_000) + "1"),
            "mobile");
        using HttpResponseMessage longEmail = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(new string('a', 100_000) + "@exemplo.pt", AuthTestAccounts.Password),
            "mobile");

        await AuthenticationTests.AssertErrorAsync(longPassword, HttpStatusCode.Unauthorized, "ERR-02", InvalidCredentials);
        await AuthenticationTests.AssertErrorAsync(longEmail, HttpStatusCode.Unauthorized, "ERR-02", InvalidCredentials);
    }

    /// <summary>
    /// Um corpo JSON mal formado é recusado com o ERR-10.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MalformedJsonReturns400()
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(AuthTestAccounts.LoginRoute, UriKind.Relative))
        {
            Content = new StringContent("{\"email\": ", Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-Client-App", "mobile");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertErr10Async(response);
    }

    /// <summary>
    /// O início de sessão é anónimo: uma credencial anterior inválida, expirada ou de conta bloqueada no cabeçalho
    /// <c>Authorization</c> não o impede.
    /// </summary>
    /// <param name="authorization">Valor do cabeçalho <c>Authorization</c>.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("Bearer isto-nao-e-um-jwt")]
    [InlineData("Bearer abc.def.ghi")]
    [InlineData("Basic YW5hOmthdGNo")]
    public async Task StaleAuthorizationHeaderDoesNotPreventSignIn(string authorization)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            "mobile",
            authorization);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Uma credencial expirada de uma sessão anterior também não impede um novo início de sessão.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ExpiredTokenOfAPreviousSessionDoesNotPreventSignIn()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);
        this.factory.Clock.Fixed = DateTimeOffset.UtcNow.AddHours(-9);
        string expired;
        try
        {
            expired = this.factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
        }
        finally
        {
            this.factory.Clock.Fixed = null;
        }

        using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            "mobile",
            $"Bearer {expired}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A resposta de sucesso nunca traz o resumo nem a palavra-passe (RNF003, RNF017).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ResponseNeverContainsThePasswordOrItsHash()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Recruiter);

        using HttpResponseMessage response = await AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(user.Email, AuthTestAccounts.Password),
            "web");

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(user.PasswordHash, body, StringComparison.Ordinal);
        Assert.DoesNotContain(AuthTestAccounts.Password, body, StringComparison.Ordinal);
        Assert.DoesNotContain(user.Email, body, StringComparison.Ordinal);
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
}
