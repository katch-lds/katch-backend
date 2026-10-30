// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes dos endpoints da decisão do Recrutador (documentação da API v02, secções 6.6.4 e 6.6.5; RF063 a RF066, RF068,
/// RF117), sobre SQLite in-memory. A perda na concorrência (<c>xmin</c>) não se exercita aqui, porque o SQLite não tem a
/// coluna de versão: está nos testes unitários do serviço e na evidência contra o PostgreSQL.
/// </summary>
public sealed class CandidateDecisionEndpointsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string ForbiddenMessage = "Não tem autorização para aceder a este elemento.";
    private const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";
    private const string ProfileNotOpenedMessage = "Abra o perfil completo do Candidato antes de registar a decisão.";
    private const string AlreadyEvaluatedMessage = "O Candidato já foi avaliado nesta vaga.";
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";
    private const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateDecisionEndpointsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public CandidateDecisionEndpointsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// Aceitar confirma o match, regista o Recrutador e a data, abre a conversa e devolve o contacto do Candidato.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AcceptConfirmsTheMatchAndReturnsTheCandidateContact()
    {
        (Guid companyId, Guid recruiterId, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);
        await this.OpenProfileAsync(match.Id, token);
        DateTimeOffset decidedAt = TruncateToSeconds(DateTimeOffset.UtcNow);
        this.factory.Clock.Fixed = decidedAt;

        using HttpResponseMessage response = await this.PostAsync($"interests/{match.Id}/accept", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(match.Id, body.GetProperty("matchId").GetGuid());
        Assert.Equal("Programador", body.GetProperty("jobTitle").GetString());
        Assert.Equal("PUBLISHED", body.GetProperty("jobStatus").GetString());
        Assert.StartsWith("Braga ", body.GetProperty("locationName").GetString(), StringComparison.Ordinal);
        Assert.EndsWith(" (Braga)", body.GetProperty("locationName").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("companyName").GetString()));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("logoUrl").ValueKind);
        Assert.Equal("Ana Ferreira", body.GetProperty("counterpartName").GetString());
        Assert.Equal(candidate.User.Email, body.GetProperty("contactEmail").GetString());
        Assert.Equal("912345678", body.GetProperty("contactPhone").GetString());
        Assert.Equal(decidedAt, body.GetProperty("matchedAt").GetDateTimeOffset());

        Match saved = await CandidateEvaluationTestData.ReadMatchAsync(this.factory, match.Id);
        Assert.Equal(MatchStatus.Matched, saved.Status);
        Assert.Equal(recruiterId, saved.DecidedById);
        Assert.Equal(decidedAt, saved.RecruiterActionAt);
        Assert.Equal(ConversationStatus.Open, saved.ConversationStatus);
        Assert.Null(saved.ConversationClosedAt);
        Assert.NotNull(saved.ProfileOpenedAt);
    }

    /// <summary>
    /// Recusar devolve 204 sem corpo, regista o Recrutador e a data e não cria conversa; o Candidato sai da lista de
    /// espera dessa vaga, mas continua na de outra vaga em que manifestou interesse (RF064).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectReturns204AndRemovesTheCandidateFromThatJobOnly()
    {
        (Guid companyId, Guid recruiterId, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        Job otherJob = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);
        Match otherMatch = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, otherJob.Id);
        await this.OpenProfileAsync(match.Id, token);
        DateTimeOffset decidedAt = TruncateToSeconds(DateTimeOffset.UtcNow);
        this.factory.Clock.Fixed = decidedAt;

        using HttpResponseMessage response = await this.PostAsync($"interests/{match.Id}/reject", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Match saved = await CandidateEvaluationTestData.ReadMatchAsync(this.factory, match.Id);
        Assert.Equal(MatchStatus.RejectedByRecruiter, saved.Status);
        Assert.Equal(recruiterId, saved.DecidedById);
        Assert.Equal(decidedAt, saved.RecruiterActionAt);
        Assert.Null(saved.ConversationStatus);

        using HttpResponseMessage rejectedJobList = await this.GetAsync($"jobs/{job.Id}/waiting-candidates", token);
        using HttpResponseMessage otherJobList = await this.GetAsync($"jobs/{otherJob.Id}/waiting-candidates", token);
        Assert.Equal(0, (await AuthTestAccounts.ReadJsonAsync(rejectedJobList)).GetArrayLength());
        Assert.Equal(
            [otherMatch.Id],
            (await AuthTestAccounts.ReadJsonAsync(otherJobList)).EnumerateArray().Select(item => item.GetProperty("matchId").GetGuid()));
        Assert.Equal(MatchStatus.Waiting, (await CandidateEvaluationTestData.ReadMatchAsync(this.factory, otherMatch.Id)).Status);
    }

    /// <summary>
    /// Sem a abertura do perfil registada, aceitar ou recusar dá 409 ERR-34 e o interesse continua em espera.
    /// </summary>
    /// <param name="action">Decisão pedida.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("accept")]
    [InlineData("reject")]
    public async Task DecisionWithoutOpenedProfileReturnsErr34AndChangesNothing(string action)
    {
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);

        using HttpResponseMessage response = await this.PostAsync($"interests/{match.Id}/{action}", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Conflict, "ERR-34", ProfileNotOpenedMessage);
        Match saved = await CandidateEvaluationTestData.ReadMatchAsync(this.factory, match.Id);
        Assert.Equal(MatchStatus.Waiting, saved.Status);
        Assert.Null(saved.DecidedById);
        Assert.Null(saved.RecruiterActionAt);
        Assert.Null(saved.ConversationStatus);
        Assert.Null(saved.ProfileOpenedAt);
    }

    /// <summary>
    /// A abertura do perfil de uma vaga não permite decidir o mesmo Candidato noutra vaga (RF118).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TheOpeningOfAnotherJobDoesNotAllowTheDecision()
    {
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        Job otherJob = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match opened = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);
        Match notOpened = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, otherJob.Id);
        await this.OpenProfileAsync(opened.Id, token);

        using HttpResponseMessage response = await this.PostAsync($"interests/{notOpened.Id}/accept", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Conflict, "ERR-34", ProfileNotOpenedMessage);
        Assert.Equal(MatchStatus.Waiting, (await CandidateEvaluationTestData.ReadMatchAsync(this.factory, notOpened.Id)).Status);
    }

    /// <summary>
    /// Uma decisão registada não se altera: qualquer segunda decisão dá 409 ERR-35 e mantém a primeira (RF117).
    /// </summary>
    /// <param name="first">Primeira decisão.</param>
    /// <param name="second">Segunda decisão.</param>
    /// <param name="expected">Estado que fica registado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("accept", "accept", MatchStatus.Matched)]
    [InlineData("accept", "reject", MatchStatus.Matched)]
    [InlineData("reject", "accept", MatchStatus.RejectedByRecruiter)]
    [InlineData("reject", "reject", MatchStatus.RejectedByRecruiter)]
    public async Task ARegisteredDecisionIsNeverChanged(string first, string second, MatchStatus expected)
    {
        (Guid companyId, Guid recruiterId, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);
        await this.OpenProfileAsync(match.Id, token);
        DateTimeOffset decidedAt = TruncateToSeconds(DateTimeOffset.UtcNow);
        this.factory.Clock.Fixed = decidedAt;
        using HttpResponseMessage firstResponse = await this.PostAsync($"interests/{match.Id}/{first}", token);
        Assert.True(firstResponse.IsSuccessStatusCode);
        this.factory.Clock.Fixed = decidedAt.AddHours(1);

        using HttpResponseMessage response = await this.PostAsync($"interests/{match.Id}/{second}", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Conflict, "ERR-35", AlreadyEvaluatedMessage);
        Match saved = await CandidateEvaluationTestData.ReadMatchAsync(this.factory, match.Id);
        Assert.Equal(expected, saved.Status);
        Assert.Equal(recruiterId, saved.DecidedById);
        Assert.Equal(decidedAt, saved.RecruiterActionAt);
        Assert.Equal(expected == MatchStatus.Matched ? ConversationStatus.Open : (ConversationStatus?)null, saved.ConversationStatus);
    }

    /// <summary>
    /// Decidir um Candidato que recusou a vaga, ou um interesse inexistente, dá 404 ERR-20.
    /// </summary>
    /// <param name="action">Decisão pedida.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("accept")]
    [InlineData("reject")]
    public async Task DecisionOnADeclinedJobOrAnUnknownInterestReturnsErr20(string action)
    {
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match declined = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id, MatchStatus.DeclinedByCandidate);

        using HttpResponseMessage onDeclined = await this.PostAsync($"interests/{declined.Id}/{action}", token);
        using HttpResponseMessage onUnknown = await this.PostAsync($"interests/{Guid.NewGuid()}/{action}", token);

        await AuthenticationTests.AssertErrorAsync(onDeclined, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
        await AuthenticationTests.AssertErrorAsync(onUnknown, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
        Assert.Equal(MatchStatus.DeclinedByCandidate, (await CandidateEvaluationTestData.ReadMatchAsync(this.factory, declined.Id)).Status);
    }

    /// <summary>
    /// O Recrutador de outra Empresa dá 403 ERR-08, mesmo com o perfil aberto pelo da Empresa da vaga, e nada é alterado.
    /// </summary>
    /// <param name="action">Decisão pedida.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("accept")]
    [InlineData("reject")]
    public async Task ARecruiterOfAnotherCompanyReturnsErr08AndChangesNothing(string action)
    {
        (Guid companyId, _, string ownerToken) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        (_, _, string otherToken) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);
        await this.OpenProfileAsync(match.Id, ownerToken);

        using HttpResponseMessage response = await this.PostAsync($"interests/{match.Id}/{action}", otherToken);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-08", ForbiddenMessage);
        Match saved = await CandidateEvaluationTestData.ReadMatchAsync(this.factory, match.Id);
        Assert.Equal(MatchStatus.Waiting, saved.Status);
        Assert.Null(saved.DecidedById);
        Assert.Null(saved.ConversationStatus);
    }

    /// <summary>
    /// Um Recrutador com a Empresa por aprovar, suspensa ou sem Empresa recebe 403 ERR-07.
    /// </summary>
    /// <param name="status">Estado da Empresa, ou <see langword="null"/> se não houver.</param>
    /// <param name="action">Decisão pedida.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending, "accept")]
    [InlineData(CompanyStatus.Pending, "reject")]
    [InlineData(CompanyStatus.Suspended, "accept")]
    [InlineData(null, "reject")]
    public async Task RecruiterWithoutApprovedCompanyReturnsErr07(CompanyStatus? status, string action)
    {
        (_, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory, status);

        using HttpResponseMessage response = await this.PostAsync($"interests/{Guid.NewGuid()}/{action}", token);

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
    /// Um Candidato ou um Administrador recebe 403 ERR-06.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="action">Decisão pedida.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, "accept")]
    [InlineData(UserType.Candidate, "reject")]
    [InlineData(UserType.Admin, "accept")]
    [InlineData(UserType.Admin, "reject")]
    public async Task OtherAccountTypesReturnErr06(UserType userType, string action)
    {
        (_, string token) = await CandidateProfileTestData.SeedAccountAsync(this.factory, userType);

        using HttpResponseMessage response = await this.PostAsync($"interests/{Guid.NewGuid()}/{action}", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-06", Err06Message);
    }

    /// <summary>
    /// Sem credencial a resposta é 401 ERR-01.
    /// </summary>
    /// <param name="action">Decisão pedida.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("accept")]
    [InlineData("reject")]
    public async Task WithoutTokenReturnsErr01(string action)
    {
        using HttpResponseMessage response = await this.PostAsync($"interests/{Guid.NewGuid()}/{action}", token: null);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSessionMessage);
    }

    /// <summary>
    /// Depois da aceitação, o Candidato deixa a lista de espera e o curriculum vitae deixa de estar disponível ao
    /// Recrutador (RNF007).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AfterTheAcceptanceTheCandidateLeavesTheWaitingList()
    {
        (Guid companyId, _, string token) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);
        await this.OpenProfileAsync(match.Id, token);

        using HttpResponseMessage accepted = await this.PostAsync($"interests/{match.Id}/accept", token);
        using HttpResponseMessage list = await this.GetAsync($"jobs/{job.Id}/waiting-candidates", token);
        using HttpResponseMessage profile = await this.GetAsync($"interests/{match.Id}/profile", token);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(0, (await AuthTestAccounts.ReadJsonAsync(list)).GetArrayLength());
        await AuthenticationTests.AssertErrorAsync(profile, HttpStatusCode.Conflict, "ERR-35", AlreadyEvaluatedMessage);
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value)
    {
        return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
    }

    private async Task OpenProfileAsync(Guid matchId, string token)
    {
        using HttpResponseMessage response = await this.GetAsync($"interests/{matchId}/profile", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> GetAsync(string path, string? token)
    {
        return CandidateProfileTestData.SendAsync(this.factory, HttpMethod.Get, $"/api/recruiter/{path}", token);
    }

    private Task<HttpResponseMessage> PostAsync(string path, string? token)
    {
        return CandidateProfileTestData.SendAsync(this.factory, HttpMethod.Post, $"/api/recruiter/{path}", token);
    }
}
