// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Net;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes da administração das contas (documentação da API v02, secção 6.10; RF075, RF085 a RF090, RF104, RF112), sobre
/// SQLite in-memory. Cobrem as listas, as transições de estado, o efeito do bloqueio e da suspensão no início de sessão,
/// na credencial já emitida e nas conversas, o registo no <c>operation_log</c> e o acesso por cada tipo de conta.
/// </summary>
public sealed class AdminAccountsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/admin/accounts";
    private const string ProbeRoute = "/test-authorization/any";
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";
    private const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";
    private const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";
    private const string BlockedMessage = "A conta está bloqueada.";
    private const string SuspendedMessage = "A conta está suspensa.";
    private const string AnyId = "f0e1d2c3-b4a5-4968-8776-a5b4c3d2e115";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminAccountsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public AdminAccountsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// Gets the five operations of the controller: method and route.
    /// </summary>
    public static TheoryData<string, string> Endpoints => new()
    {
        { "GET", Route },
        { "GET", $"{Route}/candidates" },
        { "POST", $"{Route}/{AnyId}/block" },
        { "POST", $"{Route}/candidates/{AnyId}/suspend" },
        { "POST", $"{Route}/{AnyId}/reactivate" },
    };

    /// <summary>
    /// A lista de contas tem o identificador, o correio, o tipo e o estado de cada conta, por ordem alfabética do correio
    /// (6.10.1; RF085).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListAccountsReturnsEveryAccountWithTypeAndStatusSortedByEmail()
    {
        (_, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        (Candidate candidate, _) = await AdminTestData.SeedCandidateAsync(this.factory);
        (_, AppUser recruiter, _) = await AdminTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);
        using (HttpResponseMessage blocked = await this.SendAsync(HttpMethod.Post, $"{Route}/{recruiter.Id}/block", adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
        }

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, Route, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<JsonElement> accounts = (await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().ToList();
        JsonElement candidateAccount = accounts.Single(item => item.GetProperty("userId").GetGuid() == candidate.Id);
        Assert.Equal(
            ["email", "status", "userId", "userType"],
            candidateAccount.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(candidate.User.Email, candidateAccount.GetProperty("email").GetString());
        Assert.Equal("CANDIDATE", candidateAccount.GetProperty("userType").GetString());
        Assert.Equal("ACTIVE", candidateAccount.GetProperty("status").GetString());
        JsonElement recruiterAccount = accounts.Single(item => item.GetProperty("userId").GetGuid() == recruiter.Id);
        Assert.Equal("RECRUITER", recruiterAccount.GetProperty("userType").GetString());
        Assert.Equal("BLOCKED", recruiterAccount.GetProperty("status").GetString());
        Assert.Contains(accounts, item => item.GetProperty("userType").GetString() == "ADMIN");
        List<string> emails = accounts.Select(item => item.GetProperty("email").GetString()!).ToList();
        Assert.Equal(emails.Order(StringComparer.OrdinalIgnoreCase), emails);
    }

    /// <summary>
    /// A lista de Candidatos tem o nome, o correio, a localidade com o distrito, a data de registo e o estado, por ordem
    /// alfabética do nome (6.10.2; RF088).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListCandidatesReturnsTheFiveFieldsSortedByName()
    {
        (_, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        string town = $"Guimarães {Guid.NewGuid():N}";
        (Candidate candidate, _) = await AdminTestData.SeedCandidateAsync(this.factory, "Beatriz Sousa", town);
        await AdminTestData.SeedCandidateAsync(this.factory, "Álvaro Costa");

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, $"{Route}/candidates", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<JsonElement> list = (await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().ToList();
        JsonElement item = list.Single(element => element.GetProperty("candidateId").GetGuid() == candidate.Id);
        Assert.Equal(
            ["candidateId", "email", "fullName", "locationName", "registeredAt", "status"],
            item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Beatriz Sousa", item.GetProperty("fullName").GetString());
        Assert.Equal(candidate.User.Email, item.GetProperty("email").GetString());
        Assert.Equal($"{town} (Braga)", item.GetProperty("locationName").GetString());
        Assert.Equal(candidate.User.CreatedAt, item.GetProperty("registeredAt").GetDateTimeOffset());
        Assert.EndsWith("Z", item.GetProperty("registeredAt").GetString(), StringComparison.Ordinal);
        Assert.Equal("ACTIVE", item.GetProperty("status").GetString());
        List<string> names = list.Select(element => element.GetProperty("fullName").GetString()!).ToList();
        Assert.Equal(names.Order(StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true)), names);
    }

    /// <summary>
    /// Depois do bloqueio, a conta deixa de iniciar sessão e a credencial já emitida deixa de ser aceite (ERR-03); a
    /// reativação repõe as duas; ficam registados <c>ACCOUNT_BLOCKED</c> e <c>ACCOUNT_REACTIVATED</c> com o
    /// Administrador (RF086, RF087, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BlockedAccountCannotSignInNorUseItsTokenUntilReactivated()
    {
        (AppUser admin, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        (Candidate candidate, string token) = await AdminTestData.SeedCandidateAsync(this.factory);

        using (HttpResponseMessage blocked = await this.SendAsync(HttpMethod.Post, $"{Route}/{candidate.Id}/block", adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
        }

        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Get, ProbeRoute, token),
            HttpStatusCode.Forbidden,
            "ERR-03",
            BlockedMessage);
        await AdminTestData.AssertErrorAsync(
            await this.LoginAsync(candidate.User.Email, "mobile"),
            HttpStatusCode.Forbidden,
            "ERR-03",
            BlockedMessage);

        using (HttpResponseMessage reactivated = await this.SendAsync(HttpMethod.Post, $"{Route}/{candidate.Id}/reactivate", adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        }

        using (HttpResponseMessage probe = await this.SendAsync(HttpMethod.Get, ProbeRoute, token))
        {
            Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        }

        using (HttpResponseMessage login = await this.LoginAsync(candidate.User.Email, "mobile"))
        {
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        Assert.Equal(
            [(admin.Id, OperationType.AccountBlocked), (admin.Id, OperationType.AccountReactivated)],
            await AdminTestData.ReadOperationsAsync(this.factory, candidate.Id));
    }

    /// <summary>
    /// A suspensão de um Candidato impede o início de sessão e a credencial já emitida (ERR-04), passa as conversas
    /// abertas a consulta e regista <c>ACCOUNT_SUSPENDED</c>; depois da reativação, o Candidato volta a entrar e a
    /// conversa continua em consulta (RF089, RF090, RF075, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SuspendedCandidateLosesAccessAndTheConversationStaysClosedAfterReactivation()
    {
        (AppUser admin, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        (Candidate candidate, string token) = await AdminTestData.SeedCandidateAsync(this.factory);
        (Company company, AppUser recruiter, _) = await AdminTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);
        Job job = await AdminTestData.SeedJobAsync(this.factory, company.Id, "Programador", DateTimeOffset.UtcNow.AddDays(-1));
        Match match = await AdminTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id, MatchStatus.Matched, recruiter.Id);

        using (HttpResponseMessage suspended = await this.SendAsync(
            HttpMethod.Post,
            $"{Route}/candidates/{candidate.Id}/suspend",
            adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
        }

        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Get, ProbeRoute, token),
            HttpStatusCode.Forbidden,
            "ERR-04",
            SuspendedMessage);
        await AdminTestData.AssertErrorAsync(
            await this.LoginAsync(candidate.User.Email, "mobile"),
            HttpStatusCode.Forbidden,
            "ERR-04",
            SuspendedMessage);
        Match closed = await AdminTestData.ReadMatchAsync(this.factory, match.Id);
        Assert.Equal(ConversationStatus.Closed, closed.ConversationStatus);
        Assert.Equal(ConversationCloseReason.AccountBlockedOrSuspended, closed.ConversationCloseReason);
        Assert.Null(closed.ConversationClosedById);
        Assert.Equal(MatchStatus.Matched, closed.Status);

        using (HttpResponseMessage reactivated = await this.SendAsync(HttpMethod.Post, $"{Route}/{candidate.Id}/reactivate", adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        }

        using (HttpResponseMessage login = await this.LoginAsync(candidate.User.Email, "mobile"))
        {
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        Assert.Equal(ConversationStatus.Closed, (await AdminTestData.ReadMatchAsync(this.factory, match.Id)).ConversationStatus);
        Assert.Equal(
            [(admin.Id, OperationType.AccountSuspended), (admin.Id, OperationType.AccountReactivated)],
            await AdminTestData.ReadOperationsAsync(this.factory, candidate.Id));
    }

    /// <summary>
    /// O bloqueio de um Recrutador encerra as conversas das vagas da sua Empresa e impede o início de sessão na área de
    /// gestão web (RF086, RF075).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BlockingARecruiterClosesTheConversationsOfItsCompany()
    {
        (_, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        (Candidate candidate, _) = await AdminTestData.SeedCandidateAsync(this.factory);
        (Company company, AppUser recruiter, string recruiterToken) = await AdminTestData.SeedCompanyAsync(
            this.factory,
            CompanyStatus.Approved);
        Job job = await AdminTestData.SeedJobAsync(this.factory, company.Id, "Programador", DateTimeOffset.UtcNow.AddDays(-1));
        Match match = await AdminTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id, MatchStatus.Matched, recruiter.Id);

        using (HttpResponseMessage blocked = await this.SendAsync(HttpMethod.Post, $"{Route}/{recruiter.Id}/block", adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
        }

        Match closed = await AdminTestData.ReadMatchAsync(this.factory, match.Id);
        Assert.Equal(ConversationStatus.Closed, closed.ConversationStatus);
        Assert.Equal(ConversationCloseReason.AccountBlockedOrSuspended, closed.ConversationCloseReason);
        await AdminTestData.AssertErrorAsync(
            await this.LoginAsync(recruiter.Email, "web"),
            HttpStatusCode.Forbidden,
            "ERR-03",
            BlockedMessage);
        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Get, ProbeRoute, recruiterToken),
            HttpStatusCode.Forbidden,
            "ERR-03",
            BlockedMessage);
    }

    /// <summary>
    /// As transições não previstas dão <c>409</c> (ERR-30) com o estado atual e não ficam registadas: bloquear o
    /// Administrador, bloquear ou suspender uma conta que não está ativa e reativar uma conta ativa (6.10.3 a 6.10.5).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InvalidTransitionsReturn409WithTheCurrentState()
    {
        (AppUser admin, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        (Candidate candidate, _) = await AdminTestData.SeedCandidateAsync(this.factory);

        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Post, $"{Route}/{admin.Id}/block", adminToken),
            HttpStatusCode.Conflict,
            "ERR-30",
            "A operação não é permitida no estado atual: conta de Administrador.");
        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Post, $"{Route}/{candidate.Id}/reactivate", adminToken),
            HttpStatusCode.Conflict,
            "ERR-30",
            "A operação não é permitida no estado atual: ativa.");
        using (HttpResponseMessage blocked = await this.SendAsync(HttpMethod.Post, $"{Route}/{candidate.Id}/block", adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
        }

        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Post, $"{Route}/{candidate.Id}/block", adminToken),
            HttpStatusCode.Conflict,
            "ERR-30",
            "A operação não é permitida no estado atual: bloqueada.");
        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Post, $"{Route}/candidates/{candidate.Id}/suspend", adminToken),
            HttpStatusCode.Conflict,
            "ERR-30",
            "A operação não é permitida no estado atual: bloqueada.");
        Assert.Equal(
            [(admin.Id, OperationType.AccountBlocked)],
            await AdminTestData.ReadOperationsAsync(this.factory, candidate.Id));
        Assert.Empty(await AdminTestData.ReadOperationsAsync(this.factory, admin.Id));
    }

    /// <summary>
    /// Uma conta inexistente dá <c>404</c> (ERR-20) nas três operações; a suspensão de uma conta que não é de Candidato
    /// também (6.10.4).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownAccountsAndNonCandidateSuspensionReturn404()
    {
        (_, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        (_, AppUser recruiter, _) = await AdminTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);
        Guid unknown = Guid.NewGuid();

        foreach (string path in new[]
        {
            $"{Route}/{unknown}/block",
            $"{Route}/candidates/{unknown}/suspend",
            $"{Route}/{unknown}/reactivate",
            $"{Route}/candidates/{recruiter.Id}/suspend",
        })
        {
            await AdminTestData.AssertErrorAsync(
                await this.SendAsync(HttpMethod.Post, path, adminToken),
                HttpStatusCode.NotFound,
                "ERR-20",
                NotFoundMessage);
        }

        Assert.Empty(await AdminTestData.ReadOperationsAsync(this.factory, recruiter.Id));
    }

    /// <summary>
    /// O Candidato e o Recrutador recebem <c>403</c> (ERR-06) em todas as operações (RF104).
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="path">Rota.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task OtherAccountTypesReturnErr06(string method, string path)
    {
        (_, string candidateToken) = await AdminTestData.SeedCandidateAsync(this.factory);
        (_, _, string recruiterToken) = await AdminTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);

        foreach (string token in new[] { candidateToken, recruiterToken })
        {
            await AdminTestData.AssertErrorAsync(
                await this.SendAsync(new HttpMethod(method), path, token),
                HttpStatusCode.Forbidden,
                "ERR-06",
                Err06Message);
        }
    }

    /// <summary>
    /// Sem credencial, todas as operações dão <c>401</c> (ERR-01).
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="path">Rota.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EndpointsRequireASession(string method, string path)
    {
        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(new HttpMethod(method), path, token: null),
            HttpStatusCode.Unauthorized,
            "ERR-01",
            InvalidSessionMessage);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token)
    {
        return AdminTestData.SendAsync(this.factory, method, path, token);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string clientApp)
    {
        return AuthTestAccounts.LoginAsync(
            this.factory,
            AuthTestAccounts.Credentials(email, AuthTestAccounts.Password),
            clientApp);
    }
}
