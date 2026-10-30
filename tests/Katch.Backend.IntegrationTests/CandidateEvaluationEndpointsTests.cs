// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes dos endpoints da avaliação dos Candidatos (documentação da API v02, secções 6.6.1 a 6.6.3; RF060 a RF062,
/// RF113, RF118; RNF007), sobre SQLite in-memory e com o armazenamento numa pasta temporária.
/// </summary>
public sealed class CandidateEvaluationEndpointsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";
    private const string ForbiddenMessage = "Não tem autorização para aceder a este elemento.";
    private const string AlreadyEvaluatedMessage = "O Candidato já foi avaliado nesta vaga.";
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";
    private const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateEvaluationEndpointsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public CandidateEvaluationEndpointsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// A lista tem só os interesses em espera da vaga, do mais antigo para o mais recente, com a localidade e o distrito.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListWaitingReturnsOnlyWaitingInterestsOldestFirst()
    {
        (Guid companyId, Guid recruiterId, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        Job otherJob = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        (Candidate recent, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory, "Bruno Costa");
        (Candidate oldest, Location oldestLocation) = await CandidateEvaluationTestData.SeedCandidateAsync(
            this.factory,
            "Ana Ferreira",
            configure: candidate => candidate.DesiredRole = "Programadora");
        (Candidate declined, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory, "Carla Dias");
        (Candidate matched, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory, "Duarte Reis");
        (Candidate rejected, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory, "Eva Lopes");

        Match recentMatch = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, recent.Id, job.Id, answeredAt: now.AddDays(-1));
        Match oldestMatch = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, oldest.Id, job.Id, answeredAt: now.AddDays(-3));
        await CandidateEvaluationTestData.SeedMatchAsync(this.factory, declined.Id, job.Id, MatchStatus.DeclinedByCandidate);
        await CandidateEvaluationTestData.SeedMatchAsync(this.factory, matched.Id, job.Id, MatchStatus.Matched, recruiterId: recruiterId);
        await CandidateEvaluationTestData.SeedMatchAsync(this.factory, rejected.Id, job.Id, MatchStatus.RejectedByRecruiter, recruiterId: recruiterId);
        await CandidateEvaluationTestData.SeedMatchAsync(this.factory, recent.Id, otherJob.Id);

        using HttpResponseMessage response = await this.GetAsync($"jobs/{job.Id}/waiting-candidates", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement list = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(
            [oldestMatch.Id, recentMatch.Id],
            list.EnumerateArray().Select(item => item.GetProperty("matchId").GetGuid()));

        JsonElement first = list[0];
        Assert.Equal(oldest.Id, first.GetProperty("candidateId").GetGuid());
        Assert.Equal("Ana Ferreira", first.GetProperty("fullName").GetString());
        Assert.Equal("Programadora", first.GetProperty("desiredRole").GetString());
        Assert.Equal($"{oldestLocation.Name} (Braga)", first.GetProperty("locationName").GetString());
        Assert.Equal(now.AddDays(-3), first.GetProperty("interestAt").GetDateTimeOffset(), TimeSpan.FromMilliseconds(1));
        Assert.Equal(JsonValueKind.Null, list[1].GetProperty("desiredRole").ValueKind);
    }

    /// <summary>
    /// Uma vaga sem candidatos em espera devolve a lista vazia.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListWaitingReturnsAnEmptyListWhenNobodyIsWaiting()
    {
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);

        using HttpResponseMessage response = await this.GetAsync($"jobs/{job.Id}/waiting-candidates", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await AuthTestAccounts.ReadJsonAsync(response)).GetArrayLength());
    }

    /// <summary>
    /// Uma vaga inexistente responde ERR-20.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListWaitingOfAnUnknownJobReturnsErr20()
    {
        (_, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);

        using HttpResponseMessage response = await this.GetAsync($"jobs/{Guid.NewGuid()}/waiting-candidates", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
    }

    /// <summary>
    /// A primeira abertura devolve o perfil completo e regista a data e hora; as seguintes mantêm a primeira (RF118).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileRecordsOnlyTheFirstOpening()
    {
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, Location location) = await CandidateEvaluationTestData.SeedCandidateAsync(
            this.factory,
            configure: candidate =>
            {
                candidate.DesiredRole = "Programadora";
                candidate.ExperienceSummary = "Cinco anos em backend.";
                candidate.SetLinks(["https://github.com/ana"]);
            });
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);
        DateTimeOffset firstOpening = TruncateToSeconds(DateTimeOffset.UtcNow);
        this.factory.Clock.Fixed = firstOpening;

        using HttpResponseMessage first = await this.GetAsync($"interests/{match.Id}/profile", token);
        this.factory.Clock.Fixed = firstOpening.AddMinutes(30);
        using HttpResponseMessage second = await this.GetAsync($"interests/{match.Id}/profile", token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        JsonElement profile = await AuthTestAccounts.ReadJsonAsync(first);
        Assert.Equal(match.Id, profile.GetProperty("matchId").GetGuid());
        Assert.Equal("Ana Ferreira", profile.GetProperty("fullName").GetString());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("photoUrl").ValueKind);
        Assert.Equal("Programadora", profile.GetProperty("desiredRole").GetString());
        Assert.Equal($"{location.Name} (Braga)", profile.GetProperty("locationName").GetString());
        Assert.Equal("Cinco anos em backend.", profile.GetProperty("experienceSummary").GetString());
        Assert.Equal(["https://github.com/ana"], profile.GetProperty("links").EnumerateArray().Select(link => link.GetString()!));
        Assert.False(profile.GetProperty("hasCv").GetBoolean());
        Assert.Equal(firstOpening, profile.GetProperty("profileOpenedAt").GetDateTimeOffset());

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        JsonElement again = await AuthTestAccounts.ReadJsonAsync(second);
        Assert.Equal(firstOpening, again.GetProperty("profileOpenedAt").GetDateTimeOffset());

        Match saved = await CandidateEvaluationTestData.ReadMatchAsync(this.factory, match.Id);
        Assert.Equal(firstOpening, saved.ProfileOpenedAt);
        Assert.Equal(MatchStatus.Waiting, saved.Status);
    }

    /// <summary>
    /// A abertura é registada por vaga: o interesse do mesmo Candidato noutra vaga continua sem abertura.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileRecordsTheOpeningPerJob()
    {
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        Job otherJob = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match opened = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);
        Match other = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, otherJob.Id);

        using HttpResponseMessage response = await this.GetAsync($"interests/{opened.Id}/profile", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull((await CandidateEvaluationTestData.ReadMatchAsync(this.factory, opened.Id)).ProfileOpenedAt);
        Assert.Null((await CandidateEvaluationTestData.ReadMatchAsync(this.factory, other.Id)).ProfileOpenedAt);
    }

    /// <summary>
    /// A vaga pede Java e SQL e o Candidato tem SQL e Python: só SQL fica destacada (RF062).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileHighlightsTheSkillsTheJobRequires()
    {
        Skill java = await CandidateEvaluationTestData.SeedSkillAsync(this.factory, "Java");
        Skill sql = await CandidateEvaluationTestData.SeedSkillAsync(this.factory, "SQL");
        Skill python = await CandidateEvaluationTestData.SeedSkillAsync(this.factory, "Python");
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId, java, sql);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory, skills: [sql, python]);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);

        using HttpResponseMessage response = await this.GetAsync($"interests/{match.Id}/profile", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement profile = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(
            [("Python", false), ("SQL", true)],
            profile.GetProperty("skills").EnumerateArray()
                .Select(skill => (skill.GetProperty("name").GetString()!, skill.GetProperty("matchesJob").GetBoolean())));
    }

    /// <summary>
    /// A recusa da vaga pelo Candidato não é um interesse: o perfil e o curriculum vitae respondem ERR-20.
    /// </summary>
    /// <param name="resource">Recurso pedido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("profile")]
    [InlineData("cv")]
    public async Task ProfileWithoutInterestReturnsErr20(string resource)
    {
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match declined = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id, MatchStatus.DeclinedByCandidate);

        using HttpResponseMessage response = await this.GetAsync($"interests/{declined.Id}/{resource}", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
        Assert.Null((await CandidateEvaluationTestData.ReadMatchAsync(this.factory, declined.Id)).ProfileOpenedAt);
    }

    /// <summary>
    /// Um interesse inexistente responde ERR-20.
    /// </summary>
    /// <param name="resource">Recurso pedido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("profile")]
    [InlineData("cv")]
    public async Task UnknownInterestReturnsErr20(string resource)
    {
        (_, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);

        using HttpResponseMessage response = await this.GetAsync($"interests/{Guid.NewGuid()}/{resource}", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
    }

    /// <summary>
    /// Um interesse já aceite ou recusado responde ERR-35 no perfil e no curriculum vitae.
    /// </summary>
    /// <param name="status">Decisão registada.</param>
    /// <param name="resource">Recurso pedido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(MatchStatus.Matched, "profile")]
    [InlineData(MatchStatus.Matched, "cv")]
    [InlineData(MatchStatus.RejectedByRecruiter, "profile")]
    [InlineData(MatchStatus.RejectedByRecruiter, "cv")]
    public async Task InterestAlreadyEvaluatedReturnsErr35(MatchStatus status, string resource)
    {
        (Guid companyId, Guid recruiterId, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id, status, recruiterId: recruiterId);

        using HttpResponseMessage response = await this.GetAsync($"interests/{match.Id}/{resource}", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Conflict, "ERR-35", AlreadyEvaluatedMessage);
    }

    /// <summary>
    /// O curriculum vitae é descarregado como <c>cv.pdf</c>, sem cache.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetCvReturnsThePdfAsAnAttachment()
    {
        string name = await CandidateProfileTestData.StoreFileAsync(this.factory, "curriculum_vitae", ".pdf", CandidateProfileTestData.Pdf);
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(
            this.factory,
            configure: candidate => candidate.CvPath = $"curriculum_vitae/{name}");
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);

        using HttpResponseMessage response = await this.GetAsync($"interests/{match.Id}/cv", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("cv.pdf", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(CandidateProfileTestData.Pdf, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Um Candidato sem curriculum vitae responde ERR-20, e o perfil indica <c>hasCv</c> a falso.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetCvWithoutCvReturnsErr20()
    {
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);

        using HttpResponseMessage response = await this.GetAsync($"interests/{match.Id}/cv", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
    }

    /// <summary>
    /// A vaga ou o interesse de outra Empresa respondem ERR-08 nos três endpoints, sem registar a abertura.
    /// </summary>
    /// <param name="resource">Recurso pedido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("waiting-candidates")]
    [InlineData("profile")]
    [InlineData("cv")]
    public async Task ResourcesOfAnotherCompanyReturnErr08(string resource)
    {
        (Guid otherCompanyId, _, _) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        (_, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, otherCompanyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);
        string path = resource == "waiting-candidates" ? $"jobs/{job.Id}/waiting-candidates" : $"interests/{match.Id}/{resource}";

        using HttpResponseMessage response = await this.GetAsync(path, token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-08", ForbiddenMessage);
        Assert.Null((await CandidateEvaluationTestData.ReadMatchAsync(this.factory, match.Id)).ProfileOpenedAt);
    }

    /// <summary>
    /// Um Recrutador com a Empresa por aprovar, suspensa ou sem Empresa recebe o ERR-07 nos três endpoints.
    /// </summary>
    /// <param name="status">Estado da Empresa, ou <see langword="null"/> se não houver.</param>
    /// <param name="resource">Recurso pedido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending, "waiting-candidates")]
    [InlineData(CompanyStatus.Pending, "profile")]
    [InlineData(CompanyStatus.Pending, "cv")]
    [InlineData(CompanyStatus.Suspended, "profile")]
    [InlineData(null, "profile")]
    public async Task RecruiterWithoutApprovedCompanyReturnsErr07(CompanyStatus? status, string resource)
    {
        (_, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory, status);

        using HttpResponseMessage response = await this.GetAsync(Path(resource), token);

        string label = status switch
        {
            CompanyStatus.Pending => "pendente",
            CompanyStatus.Suspended => "suspensa",
            _ => "não registada",
        };
        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Forbidden,
            "ERR-07",
            $"A operação exige a Empresa aprovada. Estado atual da Empresa: {label}.");
    }

    /// <summary>
    /// Um Candidato ou um Administrador recebe o ERR-06 nos três endpoints.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="resource">Recurso pedido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, "waiting-candidates")]
    [InlineData(UserType.Candidate, "profile")]
    [InlineData(UserType.Candidate, "cv")]
    [InlineData(UserType.Admin, "waiting-candidates")]
    [InlineData(UserType.Admin, "profile")]
    [InlineData(UserType.Admin, "cv")]
    public async Task OtherAccountTypesReturnErr06(UserType userType, string resource)
    {
        (_, string token) = await CandidateProfileTestData.SeedAccountAsync(this.factory, userType);

        using HttpResponseMessage response = await this.GetAsync(Path(resource), token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-06", Err06Message);
    }

    /// <summary>
    /// Sem credencial a resposta é o ERR-01 nos três endpoints.
    /// </summary>
    /// <param name="resource">Recurso pedido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("waiting-candidates")]
    [InlineData("profile")]
    [InlineData("cv")]
    public async Task WithoutTokenReturnsErr01(string resource)
    {
        using HttpResponseMessage response = await this.GetAsync(Path(resource), token: null);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSessionMessage);
    }

    private static string Path(string resource)
    {
        return resource == "waiting-candidates"
            ? $"jobs/{Guid.NewGuid()}/waiting-candidates"
            : $"interests/{Guid.NewGuid()}/{resource}";
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value)
    {
        return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
    }

    private Task<HttpResponseMessage> GetAsync(string path, string? token)
    {
        return CandidateProfileTestData.SendAsync(this.factory, HttpMethod.Get, $"/api/recruiter/{path}", token);
    }
}
