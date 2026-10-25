// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes dos endpoints de resposta do Candidato às vagas e de consulta da quota (documentação da API v02, secções 6.3.4
/// a 6.3.6), sobre SQLite in-memory com o relógio fixado em cada teste. A marca de concorrência <c>xmin</c> e o
/// índice <c>uq_match_candidate_job</c> sob concorrência são exercitados nos testes unitários, porque o SQLite não os
/// tem.
/// </summary>
public sealed class JobExplorationTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";
    private const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";
    private const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";
    private const string AlreadyAnsweredMessage = "Já respondeu a esta vaga.";
    private const string UnavailableMessage = "A vaga já não está disponível.";

    private static long taxIdCounter = 600_000_000;

    private readonly KatchWebApplicationFactory factory;
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow.AddTicks(-(DateTimeOffset.UtcNow.Ticks % TimeSpan.TicksPerSecond));

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExplorationTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public JobExplorationTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;

        // A fábrica é partilhada: cada teste começa com o relógio do sistema e fixa o instante depois de emitir a credencial.
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// A recusa responde 204, regista <c>DECLINED_BY_CANDIDATE</c> com a data e não mexe na quota.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DeclineReturns204AndRecordsTheDeclineWithoutTouchingTheQuota()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        Guid jobId = await this.SeedJobAsync();
        this.factory.Clock.Fixed = this.now;

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{jobId}/decline", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Match match = Assert.Single(await this.ReadMatchesAsync(candidate.Id));
        Assert.Equal(MatchStatus.DeclinedByCandidate, match.Status);
        Assert.Equal(jobId, match.JobId);
        Assert.Equal(this.now, match.CandidateActionAt);
        Assert.Equal(Candidate.MaxInterests, (await this.ReadCandidateAsync(candidate.Id)).SwipeRightsRemaining);
    }

    /// <summary>
    /// Quinze recusas seguidas não têm limite nem consomem a quota.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task FifteenDeclinesInARowDoNotChangeTheQuota()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        this.factory.Clock.Fixed = this.now;

        for (int index = 0; index < 15; index++)
        {
            Guid jobId = await this.SeedJobAsync();
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{jobId}/decline", token);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal(15, (await this.ReadMatchesAsync(candidate.Id)).Count);
        using HttpResponseMessage quota = await this.SendAsync(HttpMethod.Get, "quota", token);
        await AssertQuotaAsync(quota, 10, blockedUntil: null);
    }

    /// <summary>
    /// O interesse responde 200 com a quota atualizada, cria a linha em espera e grava a quota do Candidato.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InterestReturns200WithTheUpdatedQuotaAndRecordsAWaitingMatch()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        Guid jobId = await this.SeedJobAsync();
        this.factory.Clock.Fixed = this.now;

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{jobId}/interest", token);

        await AssertQuotaAsync(response, 9, blockedUntil: null);
        Match match = Assert.Single(await this.ReadMatchesAsync(candidate.Id));
        Assert.Equal(MatchStatus.Waiting, match.Status);
        Assert.Equal(jobId, match.JobId);
        Assert.Equal(this.now, match.CandidateActionAt);
        Assert.Equal(9, (await this.ReadCandidateAsync(candidate.Id)).SwipeRightsRemaining);
    }

    /// <summary>
    /// Dez interesses seguidos esgotam a quota e o décimo traz o fim do bloqueio, 24 horas depois.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TenthInterestInARowCarriesBlockedUntil()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        this.factory.Clock.Fixed = this.now;

        for (int index = 1; index <= 10; index++)
        {
            Guid jobId = await this.SeedJobAsync();
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{jobId}/interest", token);

            await AssertQuotaAsync(response, 10 - index, index == 10 ? this.now.AddHours(24) : null);
        }

        Assert.Equal(10, (await this.ReadMatchesAsync(candidate.Id)).Count);
        Candidate saved = await this.ReadCandidateAsync(candidate.Id);
        Assert.Equal(0, saved.SwipeRightsRemaining);
        Assert.Equal(this.now.AddHours(24), saved.NextSwipeAvailableAt);
    }

    /// <summary>
    /// O décimo primeiro interesse responde ERR-31 com o tempo em falta e <c>blockedUntil</c>, sem nova linha de match.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EleventhInterestReturnsErr31WithTheRemainingTimeAndNoNewRow()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        this.factory.Clock.Fixed = this.now;
        for (int index = 0; index < 10; index++)
        {
            using HttpResponseMessage accepted = await this.SendAsync(HttpMethod.Post, $"{await this.SeedJobAsync()}/interest", token);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        Guid eleventh = await this.SeedJobAsync();
        this.factory.Clock.Fixed = this.now.AddMinutes(13);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{eleventh}/interest", token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement root = json.RootElement;
        Assert.Equal("ERR-31", root.GetProperty("code").GetString());
        Assert.Equal(
            "Esgotou a quota de interesses. Pode voltar a manifestar interesse dentro de 23 h 47 min.",
            root.GetProperty("message").GetString());
        Assert.Equal(this.now.AddHours(24), root.GetProperty("blockedUntil").GetDateTimeOffset());
        Assert.Equal(0, root.GetProperty("fields").GetArrayLength());
        Assert.Equal(10, (await this.ReadMatchesAsync(candidate.Id)).Count);
        Assert.DoesNotContain(await this.ReadMatchesAsync(candidate.Id), match => match.JobId == eleventh);
    }

    /// <summary>
    /// Passadas 24 horas o interesse volta a ser aceite e a quota repõe-se antes de consumir.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InterestIsAcceptedAgainAfterTheBlockPeriod()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        this.factory.Clock.Fixed = this.now;
        for (int index = 0; index < 10; index++)
        {
            using HttpResponseMessage accepted = await this.SendAsync(HttpMethod.Post, $"{await this.SeedJobAsync()}/interest", token);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        Guid jobId = await this.SeedJobAsync();
        this.factory.Clock.Fixed = this.now.AddHours(24);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{jobId}/interest", token);

        await AssertQuotaAsync(response, 9, blockedUntil: null);
        Candidate saved = await this.ReadCandidateAsync(candidate.Id);
        Assert.Equal(9, saved.SwipeRightsRemaining);
        Assert.Null(saved.NextSwipeAvailableAt);
        Assert.Equal(11, (await this.ReadMatchesAsync(candidate.Id)).Count);
    }

    /// <summary>
    /// A segunda resposta à mesma vaga, em qualquer combinação de recusa e interesse, é ERR-32 e não muda nada.
    /// </summary>
    /// <param name="first">Primeira ação.</param>
    /// <param name="second">Segunda ação.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("interest", "interest")]
    [InlineData("interest", "decline")]
    [InlineData("decline", "interest")]
    [InlineData("decline", "decline")]
    public async Task SecondAnswerToTheSameJobReturnsErr32(string first, string second)
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        Guid jobId = await this.SeedJobAsync();
        this.factory.Clock.Fixed = this.now;
        using HttpResponseMessage firstResponse = await this.SendAsync(HttpMethod.Post, $"{jobId}/{first}", token);
        Assert.True(firstResponse.IsSuccessStatusCode);
        Candidate before = await this.ReadCandidateAsync(candidate.Id);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{jobId}/{second}", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Conflict, "ERR-32", AlreadyAnsweredMessage);
        Assert.Single(await this.ReadMatchesAsync(candidate.Id));
        Assert.Equal(before.SwipeRightsRemaining, (await this.ReadCandidateAsync(candidate.Id)).SwipeRightsRemaining);
    }

    /// <summary>
    /// Uma vaga inexistente, eliminada ou nunca publicada responde ERR-20, e nada é registado.
    /// </summary>
    /// <param name="action">Ação pedida.</param>
    /// <param name="scenario">Estado da vaga.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("decline", "missing")]
    [InlineData("decline", "deleted")]
    [InlineData("decline", "never-published")]
    [InlineData("interest", "missing")]
    [InlineData("interest", "deleted")]
    [InlineData("interest", "never-published")]
    public async Task MissingDeletedOrNeverPublishedJobReturnsErr20(string action, string scenario)
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        Guid jobId = scenario switch
        {
            "missing" => Guid.NewGuid(),
            "deleted" => await this.SeedJobAsync(job => job.MarkDeleted(this.now.AddDays(-1))),
            _ => await this.SeedJobAsync(publish: false),
        };
        this.factory.Clock.Fixed = this.now;

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{jobId}/{action}", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
        Assert.Empty(await this.ReadMatchesAsync(candidate.Id));
        Assert.Equal(Candidate.MaxInterests, (await this.ReadCandidateAsync(candidate.Id)).SwipeRightsRemaining);
    }

    /// <summary>
    /// Uma vaga suspensa, encerrada ou expirada, ou de uma Empresa que deixou de estar aprovada, responde ERR-33 e a
    /// quota não muda.
    /// </summary>
    /// <param name="action">Ação pedida.</param>
    /// <param name="scenario">Motivo da indisponibilidade.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("decline", "suspended-job")]
    [InlineData("decline", "closed-job")]
    [InlineData("decline", "expired-job")]
    [InlineData("decline", "suspended-company")]
    [InlineData("interest", "suspended-job")]
    [InlineData("interest", "closed-job")]
    [InlineData("interest", "expired-job")]
    [InlineData("interest", "suspended-company")]
    public async Task JobThatIsNoLongerAvailableReturnsErr33(string action, string scenario)
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        Guid jobId = scenario switch
        {
            "suspended-job" => await this.SeedJobAsync(job => job.Suspend(this.now.AddDays(-1))),
            "closed-job" => await this.SeedJobAsync(job => job.Close()),
            "expired-job" => await this.SeedJobAsync(expiresAt: this.now.AddDays(-1)),
            _ => await this.SeedJobAsync(company: CompanyStatus.Suspended),
        };
        this.factory.Clock.Fixed = this.now;

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{jobId}/{action}", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Conflict, "ERR-33", UnavailableMessage);
        Assert.Empty(await this.ReadMatchesAsync(candidate.Id));
        Assert.Equal(Candidate.MaxInterests, (await this.ReadCandidateAsync(candidate.Id)).SwipeRightsRemaining);
    }

    /// <summary>
    /// A consulta da quota com 4 interesses consumidos devolve 6 disponíveis, o total e nenhum bloqueio.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task QuotaAfterFourInterestsHasSixAvailable()
    {
        (_, string token) = await this.SeedCandidateAsync(interests: 4, lastInterest: this.now);
        this.factory.Clock.Fixed = this.now;

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "quota", token);

        await AssertQuotaAsync(response, 6, blockedUntil: null);
    }

    /// <summary>
    /// A consulta repõe e grava a quota cujo bloqueio já terminou, antes de responder (RF021).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task QuotaQueryRestoresAndPersistsAnExpiredBlock()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync(interests: 10, lastInterest: this.now.AddHours(-25));
        this.factory.Clock.Fixed = this.now;

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "quota", token);

        await AssertQuotaAsync(response, 10, blockedUntil: null);
        Candidate saved = await this.ReadCandidateAsync(candidate.Id);
        Assert.Equal(Candidate.MaxInterests, saved.SwipeRightsRemaining);
        Assert.Null(saved.NextSwipeAvailableAt);
    }

    /// <summary>
    /// A consulta durante o bloqueio devolve 0 disponíveis e o fim do bloqueio, sem repor.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task QuotaQueryDuringTheBlockReturnsBlockedUntil()
    {
        DateTimeOffset lastInterest = this.now.AddHours(-1);
        (_, string token) = await this.SeedCandidateAsync(interests: 10, lastInterest: lastInterest);
        this.factory.Clock.Fixed = this.now;

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "quota", token);

        await AssertQuotaAsync(response, 0, lastInterest.AddHours(24));
    }

    /// <summary>
    /// Uma conta de Recrutador recebe o ERR-06 nos três endpoints.
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="relativePath">Caminho depois de <c>api/candidate/jobs</c>; <c>{id}</c> é um identificador de vaga.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("POST", "{id}/decline")]
    [InlineData("POST", "{id}/interest")]
    [InlineData("GET", "quota")]
    public async Task RecruiterAccountReturnsErr06(string method, string relativePath)
    {
        string token = await this.SeedRecruiterTokenAsync();

        using HttpResponseMessage response = await this.SendAsync(
            new HttpMethod(method),
            relativePath.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal),
            token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-06", Err06Message);
    }

    /// <summary>
    /// Sem credencial a resposta é o ERR-01 nos três endpoints.
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="relativePath">Caminho depois de <c>api/candidate/jobs</c>; <c>{id}</c> é um identificador de vaga.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("POST", "{id}/decline")]
    [InlineData("POST", "{id}/interest")]
    [InlineData("GET", "quota")]
    public async Task MissingTokenReturnsErr01(string method, string relativePath)
    {
        using HttpResponseMessage response = await this.SendAsync(
            new HttpMethod(method),
            relativePath.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal),
            token: null);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSessionMessage);
    }

    /// <summary>
    /// Um identificador de vaga que não é um <see cref="Guid"/> responde ERR-10.
    /// </summary>
    /// <param name="action">Ação pedida.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("decline")]
    [InlineData("interest")]
    public async Task InvalidJobIdReturnsErr10(string action)
    {
        (_, string token) = await this.SeedCandidateAsync();

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"nao-e-um-guid/{action}", token);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "ERR-10",
            "Os dados enviados não cumprem a validação automática.");
    }

    private static async Task AssertQuotaAsync(HttpResponseMessage response, int available, DateTimeOffset? blockedUntil)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement root = json.RootElement;

        Assert.Equal(
            ["available", "blockedUntil", "total"],
            root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(available, root.GetProperty("available").GetInt32());
        Assert.Equal(10, root.GetProperty("total").GetInt32());
        if (blockedUntil is null)
        {
            Assert.Equal(JsonValueKind.Null, root.GetProperty("blockedUntil").ValueKind);
        }
        else
        {
            Assert.Equal(blockedUntil.Value, root.GetProperty("blockedUntil").GetDateTimeOffset());
        }
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

    private async Task<(Candidate Candidate, string Token)> SeedCandidateAsync(int interests = 0, DateTimeOffset? lastInterest = null)
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Candidate, created, created);
        var candidate = new Candidate(user, "Ana Silva", "912345678", Guid.NewGuid());
        for (int index = 0; index < interests; index++)
        {
            candidate.ConsumeInterest(lastInterest ?? created);
        }

        await this.SaveAsync(context =>
        {
            var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
            context.Locations.Add(location);
            candidate.LocationId = location.Id;
            context.Candidates.Add(candidate);
        });
        return (candidate, this.IssueToken(user));
    }

    private async Task<string> SeedRecruiterTokenAsync()
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Recruiter, created, created);
        await this.SaveAsync(context => context.Recruiters.Add(new Recruiter(user)));
        return this.IssueToken(user);
    }

    private async Task<Guid> SeedJobAsync(
        Action<Job>? configure = null,
        CompanyStatus company = CompanyStatus.Approved,
        DateTimeOffset? expiresAt = null,
        bool publish = true)
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Recruiter, created, created);
        var recruiter = new Recruiter(user);
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        var companyEntity = new Company(recruiter.Id, Registration(location.Id), created);
        switch (company)
        {
            case CompanyStatus.Approved:
                companyEntity.Approve();
                break;
            case CompanyStatus.Suspended:
                companyEntity.Approve();
                companyEntity.Suspend();
                break;
            default:
                break;
        }

        var job = new Job(
            companyEntity.Id,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
                ExpiresAt = expiresAt,
            });
        if (publish)
        {
            job.Publish(this.now.AddDays(-10));
        }

        configure?.Invoke(job);

        await this.SaveAsync(context =>
        {
            context.Recruiters.Add(recruiter);
            context.Locations.Add(location);
            context.Companies.Add(companyEntity);
            context.Jobs.Add(job);
        });
        return job.Id;
    }

    private async Task SaveAsync(Action<KatchDbContext> add)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        add(context);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Candidate> ReadCandidateAsync(Guid candidateId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Candidates.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == candidateId, TestContext.Current.CancellationToken);
    }

    private async Task<List<Match>> ReadMatchesAsync(Guid candidateId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Matches.AsNoTracking()
            .Where(match => match.CandidateId == candidateId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private string IssueToken(AppUser user)
    {
        return this.factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, string? token)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri($"/api/candidate/jobs/{relativePath}", UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
