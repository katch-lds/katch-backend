// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes da suspensão e da reativação das Empresas e das listas globais (documentação da API v02, secções 6.11.5 a
/// 6.11.8; RF014, RF075, RF095, RF097, RF098, RF104, RF112, RF116), sobre SQLite in-memory. A apresentação das vagas é
/// verificada pelo interesse do Candidato (6.3.5), que aplica o mesmo filtro do RF014 à vaga pedida.
/// </summary>
public sealed class AdminCompaniesTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/admin/companies";
    private const string AnyId = "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a14";
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";
    private const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";
    private const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";
    private const string UnavailableMessage = "A vaga já não está disponível.";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminCompaniesTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public AdminCompaniesTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// Gets the four operations of this Issue: method and route.
    /// </summary>
    public static TheoryData<string, string> Endpoints => new()
    {
        { "POST", $"{Route}/{AnyId}/suspend" },
        { "POST", $"{Route}/{AnyId}/reactivate" },
        { "GET", Route },
        { "GET", $"{Route}/published-jobs" },
    };

    /// <summary>
    /// A suspensão deixa de apresentar as vagas da Empresa (o interesse dá ERR-33), passa as conversas abertas a consulta
    /// com <c>COMPANY_SUSPENDED</c>, mantém o match e põe o Recrutador fora da política <c>ApprovedCompany</c> (ERR-07); a
    /// reativação volta a apresentar as vagas, a conversa continua em consulta e ficam registados
    /// <c>COMPANY_SUSPENDED</c> e <c>COMPANY_REACTIVATED</c> com o Administrador (RF095, RF116, RF075, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SuspensionHidesTheJobsAndClosesTheConversationsUntilReactivation()
    {
        (AppUser admin, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        (Company company, AppUser recruiter, string recruiterToken) = await AdminTestData.SeedCompanyAsync(
            this.factory,
            CompanyStatus.Approved);
        Job job = await AdminTestData.SeedJobAsync(this.factory, company.Id, "Programador", DateTimeOffset.UtcNow.AddDays(-1));
        (Candidate matched, _) = await AdminTestData.SeedCandidateAsync(this.factory);
        Match match = await AdminTestData.SeedMatchAsync(this.factory, matched.Id, job.Id, MatchStatus.Matched, recruiter.Id);
        (_, string candidateToken) = await AdminTestData.SeedCandidateAsync(this.factory);

        using (HttpResponseMessage suspended = await this.SendAsync(HttpMethod.Post, $"{Route}/{company.Id}/suspend", adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
        }

        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Post, $"/api/candidate/jobs/{job.Id}/interest", candidateToken),
            HttpStatusCode.Conflict,
            "ERR-33",
            UnavailableMessage);
        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Get, "/test-authorization/approved-company", recruiterToken),
            HttpStatusCode.Forbidden,
            "ERR-07",
            "A operação exige a Empresa aprovada. Estado atual da Empresa: suspensa.");
        Match closed = await AdminTestData.ReadMatchAsync(this.factory, match.Id);
        Assert.Equal(MatchStatus.Matched, closed.Status);
        Assert.Equal(ConversationStatus.Closed, closed.ConversationStatus);
        Assert.Equal(ConversationCloseReason.CompanySuspended, closed.ConversationCloseReason);
        Assert.Null(closed.ConversationClosedById);

        using (HttpResponseMessage reactivated = await this.SendAsync(HttpMethod.Post, $"{Route}/{company.Id}/reactivate", adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        }

        using (HttpResponseMessage interest = await this.SendAsync(HttpMethod.Post, $"/api/candidate/jobs/{job.Id}/interest", candidateToken))
        {
            Assert.Equal(HttpStatusCode.OK, interest.StatusCode);
        }

        Assert.Equal(ConversationStatus.Closed, (await AdminTestData.ReadMatchAsync(this.factory, match.Id)).ConversationStatus);
        Assert.Equal(
            [(admin.Id, OperationType.CompanySuspended), (admin.Id, OperationType.CompanyReactivated)],
            await AdminTestData.ReadOperationsAsync(this.factory, company.Id));
    }

    /// <summary>
    /// As transições não previstas dão <c>409</c> (ERR-30) com o estado atual e não ficam registadas: suspender uma
    /// Empresa pendente, recusada ou já suspensa e reativar uma Empresa que não está suspensa (6.11.5 e 6.11.6).
    /// </summary>
    /// <param name="status">Estado atual da Empresa.</param>
    /// <param name="operation">Operação pedida.</param>
    /// <param name="label">Designação do estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending, "suspend", "pendente")]
    [InlineData(CompanyStatus.Rejected, "suspend", "recusada")]
    [InlineData(CompanyStatus.Suspended, "suspend", "suspensa")]
    [InlineData(CompanyStatus.Pending, "reactivate", "pendente")]
    [InlineData(CompanyStatus.Approved, "reactivate", "aprovada")]
    public async Task InvalidTransitionsReturn409WithTheCurrentState(CompanyStatus status, string operation, string label)
    {
        (_, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        (Company company, _, _) = await AdminTestData.SeedCompanyAsync(this.factory, status);

        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Post, $"{Route}/{company.Id}/{operation}", adminToken),
            HttpStatusCode.Conflict,
            "ERR-30",
            $"A operação não é permitida no estado atual: {label}.");
        Assert.Empty(await AdminTestData.ReadOperationsAsync(this.factory, company.Id));
    }

    /// <summary>
    /// Uma Empresa inexistente dá <c>404</c> (ERR-20) na suspensão e na reativação.
    /// </summary>
    /// <param name="operation">Operação pedida.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task UnknownCompanyReturns404(string operation)
    {
        (_, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);

        await AdminTestData.AssertErrorAsync(
            await this.SendAsync(HttpMethod.Post, $"{Route}/{Guid.NewGuid()}/{operation}", adminToken),
            HttpStatusCode.NotFound,
            "ERR-20",
            NotFoundMessage);
    }

    /// <summary>
    /// A lista global tem todas as Empresas, em qualquer estado, com a designação, o estado e o número de vagas
    /// publicadas, que não conta as vagas por publicar nem as suspensas, por ordem alfabética (6.11.7; RF097).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListCompaniesReturnsEveryCompanyWithItsPublishedJobs()
    {
        (_, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        (Company approved, _, _) = await AdminTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved, "Empresa Aprovada, Lda.");
        (Company pending, _, _) = await AdminTestData.SeedCompanyAsync(this.factory, CompanyStatus.Pending, "Empresa Pendente, Lda.");
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow.AddDays(-3);
        await AdminTestData.SeedJobAsync(this.factory, approved.Id, "Vaga 1", publishedAt);
        await AdminTestData.SeedJobAsync(this.factory, approved.Id, "Vaga 2", publishedAt);
        await AdminTestData.SeedJobAsync(this.factory, approved.Id, "Vaga suspensa", publishedAt, job => job.Suspend(publishedAt.AddDays(1)));
        await AdminTestData.SeedJobAsync(this.factory, approved.Id, "Vaga por publicar", publishedAt: null);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, Route, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<JsonElement> list = (await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().ToList();
        JsonElement first = list.Single(item => item.GetProperty("companyId").GetGuid() == approved.Id);
        Assert.Equal(
            ["companyId", "companyName", "publishedJobs", "status"],
            first.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Empresa Aprovada, Lda.", first.GetProperty("companyName").GetString());
        Assert.Equal("APPROVED", first.GetProperty("status").GetString());
        Assert.Equal(2, first.GetProperty("publishedJobs").GetInt32());
        JsonElement second = list.Single(item => item.GetProperty("companyId").GetGuid() == pending.Id);
        Assert.Equal("PENDING", second.GetProperty("status").GetString());
        Assert.Equal(0, second.GetProperty("publishedJobs").GetInt32());
        Assert.True(list.IndexOf(first) < list.IndexOf(second));
    }

    /// <summary>
    /// A lista global de vagas publicadas tem a função, a Empresa, a data de publicação e o número de interesses em
    /// todos os estados da decisão, sem as recusas, da publicação mais recente para a mais antiga, e não inclui as vagas
    /// por publicar nem as suspensas (6.11.8; RF098).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListPublishedJobsReturnsTheFourFieldsFromTheMostRecentPublication()
    {
        (_, string adminToken) = await AdminTestData.SeedAdminAsync(this.factory);
        string companyName = $"Lumen {Guid.NewGuid():N}, Lda.";
        (Company company, AppUser recruiter, _) = await AdminTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved, companyName);
        var older = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset newer = older.AddDays(5);
        Job oldJob = await AdminTestData.SeedJobAsync(this.factory, company.Id, "Programador Backend .NET", older);
        Job newJob = await AdminTestData.SeedJobAsync(this.factory, company.Id, "Técnico de Logística", newer);
        Job suspendedJob = await AdminTestData.SeedJobAsync(this.factory, company.Id, "Suspensa", older, job => job.Suspend(newer));
        Job unpublished = await AdminTestData.SeedJobAsync(this.factory, company.Id, "Por publicar", publishedAt: null);
        foreach (MatchStatus status in new[] { MatchStatus.Waiting, MatchStatus.Matched, MatchStatus.DeclinedByCandidate })
        {
            (Candidate candidate, _) = await AdminTestData.SeedCandidateAsync(this.factory);
            await AdminTestData.SeedMatchAsync(this.factory, candidate.Id, oldJob.Id, status, recruiter.Id);
        }

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, $"{Route}/published-jobs", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<JsonElement> list = (await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().ToList();
        List<JsonElement> ours = list.Where(item => item.GetProperty("companyName").GetString() == companyName).ToList();
        Assert.Equal([newJob.Id, oldJob.Id], ours.Select(item => item.GetProperty("jobId").GetGuid()));
        Assert.Equal(
            ["companyName", "interestCount", "jobId", "publishedAt", "title"],
            ours[1].EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Programador Backend .NET", ours[1].GetProperty("title").GetString());
        Assert.Equal(older, ours[1].GetProperty("publishedAt").GetDateTimeOffset());
        Assert.Equal(2, ours[1].GetProperty("interestCount").GetInt32());
        Assert.Equal(0, ours[0].GetProperty("interestCount").GetInt32());
        Assert.DoesNotContain(list, item => item.GetProperty("jobId").GetGuid() == suspendedJob.Id);
        Assert.DoesNotContain(list, item => item.GetProperty("jobId").GetGuid() == unpublished.Id);
        List<DateTimeOffset> dates = list.Select(item => item.GetProperty("publishedAt").GetDateTimeOffset()).ToList();
        Assert.Equal(dates.OrderDescending(), dates);
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
}
