// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de <c>GET /api/admin/indicators</c> (documentação da API v02, secção 6.12.1; RF096, RF103, RF104): cada
/// indicador sobre um conjunto de dados conhecido, com os casos de fronteira do período, a validação das datas e o
/// acesso por tipo de conta.
/// </summary>
public sealed class AdminIndicatorsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/admin/indicators";

    // Período de 2026-10-01 a 2026-10-09 em Lisboa (UTC+1 até 2026-10-25): de 2026-09-30 23:00 UTC, inclusive, a
    // 2026-10-09 23:00 UTC, exclusivo.
    private const string PeriodQuery = "?from=2026-10-01&to=2026-10-09";

    private const string MessageContent = "Texto confidencial da conversa de teste";

    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodStart = new(2026, 9, 30, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 10, 9, 23, 0, 0, TimeSpan.Zero);

    private static long taxIdCounter = 700_000_000;

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminIndicatorsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação de teste partilhada pelos testes de validação e de acesso.</param>
    public AdminIndicatorsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        this.factory.Clock.Fixed = Now;
    }

    /// <summary>
    /// Os sete indicadores coincidem com a contagem feita à mão sobre os dados fictícios de <see cref="SeedKnownDataAsync"/>,
    /// e a resposta só tem os sete números, sem o texto das mensagens (RF096, RF103).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task IndicatorsMatchTheKnownData()
    {
        // Base de dados própria: as contagens não podem incluir os dados dos outros testes.
        using var isolated = new KatchWebApplicationFactory();
        isolated.Clock.Fixed = Now;
        AppUser admin = await SeedKnownDataAsync(isolated);

        using HttpResponseMessage response = await SendAsync(isolated, Route + PeriodQuery, admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(MessageContent, body, StringComparison.Ordinal);
        using JsonDocument json = JsonDocument.Parse(body);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["activeCandidates"] = 3,
                ["approvedCompanies"] = 3,
                ["pendingCompanies"] = 2,
                ["jobsFirstPublished"] = 4,
                ["interestsExpressed"] = 4,
                ["matchesConfirmed"] = 3,
                ["conversationsStarted"] = 2,
            },
            json.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetInt32()));
    }

    /// <summary>
    /// Sem dados no período, os indicadores de acontecimentos são zero.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PeriodWithoutEventsCountsZero()
    {
        using var isolated = new KatchWebApplicationFactory();
        isolated.Clock.Fixed = Now;
        AppUser admin = await SeedKnownDataAsync(isolated);

        using HttpResponseMessage response = await SendAsync(isolated, Route + "?from=2026-01-01&to=2026-01-31", admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement json = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.All(json.EnumerateObject(), property => Assert.Equal(0, property.Value.GetInt32()));
    }

    /// <summary>
    /// Datas em falta, mal formadas, com a inicial posterior à final ou com a final posterior à data atual são recusadas
    /// com <c>400</c> (ERR-10) e o campo em causa.
    /// </summary>
    /// <param name="query">Parâmetros do pedido.</param>
    /// <param name="expected">Campos e motivos esperados, separados por <c>|</c>.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("", "from:O campo é obrigatório.|to:O campo é obrigatório.")]
    [InlineData("?from=2026-10-01", "to:O campo é obrigatório.")]
    [InlineData("?from=&to=2026-10-09", "from:O campo é obrigatório.")]
    [InlineData("?from=01-10-2026&to=2026-10-09", "from:A data tem de ter o formato aaaa-mm-dd.")]
    [InlineData("?from=2026-10-01&to=2026-13-01", "to:A data tem de ter o formato aaaa-mm-dd.")]
    [InlineData("?from=2026-10-01T00:00:00Z&to=2026-10-09", "from:A data tem de ter o formato aaaa-mm-dd.")]
    [InlineData("?from=2026-10-10&to=2026-10-09", "from:A data inicial não pode ser posterior à data final.")]
    [InlineData("?from=2026-10-01&to=2026-10-30", "to:A data final não pode ser posterior à data atual.")]
    public async Task InvalidPeriodIsRejected(string query, string expected)
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);

        using HttpResponseMessage response = await SendAsync(this.factory, Route + query, admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-10", error.GetProperty("code").GetString());
        Assert.Equal("Os dados enviados não cumprem a validação automática.", error.GetProperty("message").GetString());
        Assert.Equal(
            expected.Split('|').Select(item => (item[..item.IndexOf(':', StringComparison.Ordinal)], item[(item.IndexOf(':', StringComparison.Ordinal) + 1)..])),
            AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// A data atual pode ser a data final, e um período de um só dia é válido.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TodayIsAValidEndDate()
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);

        using HttpResponseMessage response = await SendAsync(this.factory, Route + "?from=2026-10-29&to=2026-10-29", admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// O Candidato e o Recrutador recebem <c>403</c> (ERR-06): só o Administrador consulta os indicadores (RF104).
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Recruiter)]
    public async Task OtherAccountTypesAreForbidden(UserType userType)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, userType);

        using HttpResponseMessage response = await SendAsync(this.factory, Route + PeriodQuery, user);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-06", error.GetProperty("code").GetString());
        Assert.Equal("A operação não está disponível para este tipo de conta.", error.GetProperty("message").GetString());
    }

    /// <summary>
    /// Sem credencial, o pedido recebe <c>401</c> (ERR-01).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AnonymousRequestIsUnauthorized()
    {
        using HttpResponseMessage response = await SendAsync(this.factory, Route + PeriodQuery, user: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-01", error.GetProperty("code").GetString());
    }

    /// <summary>
    /// Conjunto de dados fictícios com o valor esperado de cada indicador no período de 2026-10-01 a 2026-10-09. Cada
    /// elemento indica, no comentário, se conta e porquê.
    /// </summary>
    private static async Task<AppUser> SeedKnownDataAsync(KatchWebApplicationFactory target)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = target.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        AppUser admin = NewUser(UserType.Admin, At(9, 1));
        context.Users.Add(admin);
        var location = new Location("Braga", "Braga", 41.545450m, -8.426507m);
        context.Locations.Add(location);

        // Candidatos ativos na data final: 3 (c1, c3, c6).
        AppUser c1 = NewUser(UserType.Candidate, At(9, 1)); // conta: ativa e sem operações
        AppUser c2 = NewUser(UserType.Candidate, At(9, 1)); // não conta: bloqueada no período
        c2.Block(At(10, 5));
        AppUser c3 = NewUser(UserType.Candidate, At(9, 1)); // conta: suspensa só depois do período
        c3.Suspend(At(10, 20));
        AppUser c4 = NewUser(UserType.Candidate, At(9, 1)); // não conta: bloqueada antes, reativada depois do período
        c4.Block(At(9, 15));
        c4.Reactivate(At(10, 15));
        AppUser c5 = NewUser(UserType.Candidate, PeriodEnd); // não conta: criada no instante final (exclusivo)
        AppUser c6 = NewUser(UserType.Candidate, PeriodEnd.AddTicks(-1)); // conta: criada no último instante
        AppUser recruiterAccount = NewUser(UserType.Recruiter, At(9, 1)); // não conta: não é Candidato
        context.Users.AddRange(c1, c2, c3, c4, c5, c6, recruiterAccount);
        context.Recruiters.Add(new Recruiter(recruiterAccount));
        context.OperationLogs.AddRange(
            OperationLog.Record(admin.Id, OperationType.AccountBlocked, c2.Id, At(10, 5)),
            OperationLog.Record(admin.Id, OperationType.AccountSuspended, c3.Id, At(10, 20)),
            OperationLog.Record(admin.Id, OperationType.AccountBlocked, c4.Id, At(9, 15)),
            OperationLog.Record(admin.Id, OperationType.AccountReactivated, c4.Id, At(10, 15)));
        var candidate = new Candidate(c1, "Ana Ferreira", "912345678", location.Id);
        context.Candidates.Add(candidate);

        // Empresas aprovadas na data final: 3 (k1, k2, k8). Pendentes: 2 (k4, k5).
        Company k1 = AddCompany(context, location.Id, At(9, 1)); // aprovada
        k1.Approve();
        LogCompany(context, admin, k1, (OperationType.CompanySubmitted, At(9, 1)), (OperationType.CompanyApproved, At(9, 2)));
        Company k2 = AddCompany(context, location.Id, At(9, 1)); // aprovada: suspensa só depois do período
        k2.Approve();
        k2.Suspend();
        LogCompany(
            context,
            admin,
            k2,
            (OperationType.CompanySubmitted, At(9, 1)),
            (OperationType.CompanyApproved, At(9, 2)),
            (OperationType.CompanySuspended, At(10, 12)));
        Company k3 = AddCompany(context, location.Id, At(9, 1)); // não conta: suspensa no período
        k3.Approve();
        k3.Suspend();
        LogCompany(
            context,
            admin,
            k3,
            (OperationType.CompanySubmitted, At(9, 1)),
            (OperationType.CompanyApproved, At(9, 2)),
            (OperationType.CompanySuspended, At(10, 3)));
        Company k4 = AddCompany(context, location.Id, At(10, 2)); // pendente: submetida no período
        LogCompany(context, admin, k4, (OperationType.CompanySubmitted, At(10, 2)));
        Company k5 = AddCompany(context, location.Id, At(10, 5)); // pendente: aprovada só depois do período
        k5.Approve();
        LogCompany(context, admin, k5, (OperationType.CompanySubmitted, At(10, 5)), (OperationType.CompanyApproved, At(10, 15)));
        Company k6 = AddCompany(context, location.Id, At(10, 12)); // não conta: submetida depois do período
        LogCompany(context, admin, k6, (OperationType.CompanySubmitted, At(10, 12)));
        Company k7 = AddCompany(context, location.Id, At(9, 1)); // não conta: recusada na data final, nova submissão depois
        k7.Reject("Documentação em falta no pedido.");
        k7.Resubmit(Registration(location.Id), At(10, 20));
        LogCompany(
            context,
            admin,
            k7,
            (OperationType.CompanySubmitted, At(9, 1)),
            (OperationType.CompanyRejected, At(9, 3)),
            (OperationType.CompanySubmitted, At(10, 20)));
        Company k8 = AddCompany(context, location.Id, At(9, 10)); // aprovada: inserida sem registo (dados por SQL)
        k8.Approve();

        // Vagas publicadas pela primeira vez no período: 4 (j1, j3, j4, j6).
        AddJob(context, k1, location.Id, PeriodStart); // j1 conta: primeiro instante (inclusive)
        AddJob(context, k1, location.Id, PeriodStart.AddTicks(-1)); // j2 não conta: antes do período
        Job j3 = AddJob(context, k1, location.Id, At(10, 5)); // j3 conta: nova publicação depois não muda a primeira
        j3.Suspend(At(10, 6));
        j3.Republish(At(10, 20));
        AddJob(context, k1, location.Id, PeriodEnd.AddTicks(-1)); // j4 conta: último instante
        AddJob(context, k1, location.Id, PeriodEnd); // j5 não conta: instante final (exclusivo)
        Job j6 = AddJob(context, k1, location.Id, At(10, 3)); // j6 conta: eliminada depois, mas foi publicada
        j6.MarkDeleted(At(10, 4));
        AddJob(context, k1, location.Id, publishedAt: null); // j7 não conta: nunca publicada

        // Interesses no período: 4 (m1, m4, m5, m6). Matches no período: 3 (m3, m6, m8).
        // Conversas iniciadas no período: 2 (m3, m8). As vagas destas respostas foram publicadas antes do período.
        Guid recruiterId = k1.RecruiterId;
        Interest(context, candidate, k1, location.Id, At(10, 2)); // m1 interesse em espera
        context.Matches.Add(Match.Decline(candidate.Id, AddJob(context, k1, location.Id, At(9, 20)).Id, At(10, 3))); // m2 recusa
        Match m3 = Interest(context, candidate, k1, location.Id, At(9, 25)); // interesse antes; match no período
        Accept(m3, recruiterId, At(10, 4));
        Match m4 = Interest(context, candidate, k1, location.Id, At(10, 5)); // interesse no período; match depois
        Accept(m4, recruiterId, At(10, 12));
        Match m5 = Interest(context, candidate, k1, location.Id, At(10, 6)); // interesse no período; recusado
        m5.OpenProfile(At(10, 7));
        m5.Reject(recruiterId, At(10, 7));
        Match m6 = Interest(context, candidate, k1, location.Id, At(10, 1)); // interesse e match no período
        Accept(m6, recruiterId, At(10, 8));
        Match m7 = Interest(context, candidate, k1, location.Id, At(9, 1)); // interesse e match antes do período
        Accept(m7, recruiterId, At(9, 10));
        Match m8 = Interest(context, candidate, k1, location.Id, At(9, 28)); // match no período
        Accept(m8, recruiterId, At(10, 2));
        context.Messages.AddRange(
            Message.Send(m3.Id, c1.Id, MessageContent, At(10, 4)), // m3 conta: primeira mensagem no período
            Message.Send(m3.Id, recruiterId, MessageContent, At(10, 20)),
            Message.Send(m6.Id, c1.Id, MessageContent, PeriodEnd), // m6 não conta: primeira no instante final
            Message.Send(m7.Id, c1.Id, MessageContent, At(9, 15)), // m7 não conta: começou antes do período
            Message.Send(m7.Id, recruiterId, MessageContent, At(10, 5)),
            Message.Send(m8.Id, c1.Id, MessageContent, At(10, 3)), // m8 conta uma vez, com duas mensagens no período
            Message.Send(m8.Id, recruiterId, MessageContent, At(10, 6)));

        await context.SaveChangesAsync(cancellationToken);
        return admin;
    }

    private static DateTimeOffset At(int month, int day)
    {
        return new DateTimeOffset(2026, month, day, 12, 0, 0, TimeSpan.Zero);
    }

    private static AppUser NewUser(UserType userType, DateTimeOffset createdAt)
    {
        return new AppUser($"{userType.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@exemplo.pt", "hash", userType, createdAt, createdAt);
    }

    private static Company AddCompany(KatchDbContext context, Guid locationId, DateTimeOffset submittedAt)
    {
        AppUser account = NewUser(UserType.Recruiter, submittedAt);
        var recruiter = new Recruiter(account);
        var company = new Company(recruiter.Id, Registration(locationId), submittedAt);
        context.Users.Add(account);
        context.Recruiters.Add(recruiter);
        context.Companies.Add(company);
        return company;
    }

    private static void LogCompany(
        KatchDbContext context,
        AppUser admin,
        Company company,
        params (OperationType Operation, DateTimeOffset At)[] operations)
    {
        foreach ((OperationType operation, DateTimeOffset at) in operations)
        {
            Guid author = operation == OperationType.CompanySubmitted ? company.RecruiterId : admin.Id;
            context.OperationLogs.Add(OperationLog.Record(author, operation, company.Id, at));
        }
    }

    private static Job AddJob(KatchDbContext context, Company company, Guid locationId, DateTimeOffset? publishedAt)
    {
        var job = new Job(
            company.Id,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = locationId,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        if (publishedAt is not null)
        {
            job.Publish(publishedAt.Value);
        }

        context.Jobs.Add(job);
        return job;
    }

    private static Match Interest(KatchDbContext context, Candidate candidate, Company company, Guid locationId, DateTimeOffset at)
    {
        Job job = AddJob(context, company, locationId, At(9, 1));
        var match = Match.ExpressInterest(candidate.Id, job.Id, at);
        context.Matches.Add(match);
        return match;
    }

    private static void Accept(Match match, Guid recruiterId, DateTimeOffset at)
    {
        match.OpenProfile(at);
        match.Accept(recruiterId, at);
    }

    private static CompanyRegistration Registration(Guid locationId)
    {
        return new CompanyRegistration
        {
            CompanyName = "Empresa Exemplo, Lda.",
            TaxId = Interlocked.Increment(ref taxIdCounter).ToString(CultureInfo.InvariantCulture),
            Industry = Industry.Technology,
            Address = "Rua de Exemplo, 1, 4700-000 Braga",
            LocationId = locationId,
            ContactEmail = "geral@empresa-exemplo.pt",
            ContactPhone = "253000000",
            ResponsibleName = "Responsável Exemplo",
        };
    }

    private static async Task<HttpResponseMessage> SendAsync(KatchWebApplicationFactory target, string route, AppUser? user)
    {
        using HttpClient client = target.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));
        if (user is not null)
        {
            string token = target.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
