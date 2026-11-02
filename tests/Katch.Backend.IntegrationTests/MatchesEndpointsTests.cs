// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Text.Json;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Files;
using Microsoft.Extensions.DependencyInjection;
using static Katch.Backend.IntegrationTests.ConversationTestData;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes da consulta dos matches (documentação da API v02, secção 6.7.1), sobre SQLite in-memory: cada teste grava as
/// suas próprias contas, por isso a fábrica pode ser partilhada.
/// </summary>
public sealed class MatchesEndpointsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/matches";
    private const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchesEndpointsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public MatchesEndpointsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// O Candidato recebe o seu match com os dados da vaga, da Empresa e os contactos de registo da Empresa (RF024, RF025).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CandidateGetsItsMatchWithTheCompanyCounterpartAndContacts()
    {
        CompanySeed company = await SeedCompanyAsync(this.factory, logoPath: "company_logo/lumen.png");
        CandidateSeed candidate = await SeedCandidateAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, company);
        DateTimeOffset matchedAt = new(2026, 10, 9, 16, 2, 11, TimeSpan.Zero);
        Guid matchId = await SeedMatchAsync(this.factory, candidate, company, jobId, matchedAt: matchedAt);

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Get, Route, candidate.Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement item = Assert.Single((await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().ToList());
        Assert.Equal(matchId, item.GetProperty("matchId").GetGuid());
        Assert.Equal("Programador Backend .NET", item.GetProperty("jobTitle").GetString());
        Assert.Equal("PUBLISHED", item.GetProperty("jobStatus").GetString());
        Assert.Equal($"{company.Location.Name} (Braga)", item.GetProperty("locationName").GetString());
        Assert.Equal("Lumen Software, Lda.", item.GetProperty("companyName").GetString());
        Assert.Equal(this.UrlOf("company_logo/lumen.png"), item.GetProperty("logoUrl").GetString());
        Assert.Equal("Rui Matos", item.GetProperty("counterpartName").GetString());
        Assert.Equal("recrutamento@lumen-exemplo.pt", item.GetProperty("contactEmail").GetString());
        Assert.Equal("253000222", item.GetProperty("contactPhone").GetString());
        Assert.Equal(matchedAt, item.GetProperty("matchedAt").GetDateTimeOffset());
    }

    /// <summary>
    /// O Recrutador recebe os matches das vagas da sua Empresa com os dados e os contactos do Candidato (RF067).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RecruiterGetsTheCompanyMatchesWithTheCandidateCounterpartAndContacts()
    {
        CompanySeed company = await SeedCompanyAsync(this.factory);
        CandidateSeed candidate = await SeedCandidateAsync(this.factory, "Ana Ferreira", "912345678");
        Guid jobId = await SeedJobAsync(this.factory, company);
        Guid matchId = await SeedMatchAsync(this.factory, candidate, company, jobId);

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Get, Route, company.Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement item = Assert.Single((await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().ToList());
        Assert.Equal(matchId, item.GetProperty("matchId").GetGuid());
        Assert.Equal("Ana Ferreira", item.GetProperty("counterpartName").GetString());
        Assert.Equal(candidate.User.Email, item.GetProperty("contactEmail").GetString());
        Assert.Equal("912345678", item.GetProperty("contactPhone").GetString());
        Assert.Equal("Lumen Software, Lda.", item.GetProperty("companyName").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("logoUrl").ValueKind);
    }

    /// <summary>
    /// Um Candidato nunca vê os matches de outro, e o Recrutador nunca vê os de outra Empresa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EachPartyOnlySeesItsOwnMatches()
    {
        CompanySeed companyA = await SeedCompanyAsync(this.factory);
        CompanySeed companyB = await SeedCompanyAsync(this.factory);
        CandidateSeed candidateA = await SeedCandidateAsync(this.factory);
        CandidateSeed candidateB = await SeedCandidateAsync(this.factory);
        Guid jobA = await SeedJobAsync(this.factory, companyA);
        Guid jobB = await SeedJobAsync(this.factory, companyB);
        Guid matchA = await SeedMatchAsync(this.factory, candidateA, companyA, jobA);
        Guid matchB = await SeedMatchAsync(this.factory, candidateB, companyB, jobB);

        Assert.Equal([matchA], await this.ListIdsAsync(candidateA.Token));
        Assert.Equal([matchB], await this.ListIdsAsync(candidateB.Token));
        Assert.Equal([matchA], await this.ListIdsAsync(companyA.Token));
        Assert.Equal([matchB], await this.ListIdsAsync(companyB.Token));
    }

    /// <summary>
    /// Só entram os matches confirmados: a vaga recusada, o interesse em espera e o recusado pelo Recrutador ficam de fora.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OnlyConfirmedMatchesAreListed()
    {
        CompanySeed company = await SeedCompanyAsync(this.factory);
        CandidateSeed candidate = await SeedCandidateAsync(this.factory);
        Guid matched = await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "A"));
        await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "B"), MatchKind.Declined);
        await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "C"), MatchKind.Waiting);
        await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "D"), MatchKind.Rejected);

        Assert.Equal([matched], await this.ListIdsAsync(candidate.Token));
        Assert.Equal([matched], await this.ListIdsAsync(company.Token));
    }

    /// <summary>
    /// A ordem é a do <c>matchedAt</c> decrescente, e os matches de vagas encerradas e de conversas encerradas continuam
    /// na lista (RF024, RF067).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MatchesAreOrderedFromTheNewestAndKeepClosedJobsAndConversations()
    {
        CompanySeed company = await SeedCompanyAsync(this.factory);
        CandidateSeed candidate = await SeedCandidateAsync(this.factory);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid oldest = await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "A"), matchedAt: now.AddDays(-5));
        Guid newest = await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "B", JobStatus.Closed), matchedAt: now.AddDays(-1));
        Guid middle = await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "C"), MatchKind.ClosedCompany, now.AddDays(-3));

        Assert.Equal([newest, middle, oldest], await this.ListIdsAsync(candidate.Token));
        Assert.Equal([newest, middle, oldest], await this.ListIdsAsync(company.Token));

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Get, Route, candidate.Token);
        JsonElement first = (await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().First();
        Assert.Equal("CLOSED", first.GetProperty("jobStatus").GetString());
    }

    /// <summary>
    /// O Candidato continua a ver os matches de uma Empresa suspensa (RF024).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CandidateStillSeesTheMatchesOfASuspendedCompany()
    {
        CompanySeed company = await SeedCompanyAsync(this.factory, CompanyStatus.Suspended);
        CandidateSeed candidate = await SeedCandidateAsync(this.factory);
        Guid matchId = await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company), MatchKind.ClosedCompany);

        Assert.Equal([matchId], await this.ListIdsAsync(candidate.Token));
    }

    /// <summary>
    /// Sem matches a lista é vazia.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReturnsAnEmptyListWithoutMatches()
    {
        CandidateSeed candidate = await SeedCandidateAsync(this.factory);
        CompanySeed company = await SeedCompanyAsync(this.factory);

        using HttpResponseMessage candidateResponse = await SendAsync(this.factory, HttpMethod.Get, Route, candidate.Token);
        using HttpResponseMessage recruiterResponse = await SendAsync(this.factory, HttpMethod.Get, Route, company.Token);

        Assert.Equal(HttpStatusCode.OK, candidateResponse.StatusCode);
        Assert.Equal("[]", await candidateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("[]", await recruiterResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// O Recrutador com a Empresa pendente, recusada ou suspensa recebe 403 ERR-07 com o estado da Empresa.
    /// </summary>
    /// <param name="status">Estado da Empresa.</param>
    /// <param name="label">Designação do estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending, "pendente")]
    [InlineData(CompanyStatus.Rejected, "recusada")]
    [InlineData(CompanyStatus.Suspended, "suspensa")]
    public async Task RecruiterWithACompanyThatIsNotApprovedGetsErr07(CompanyStatus status, string label)
    {
        CompanySeed company = await SeedCompanyAsync(this.factory, status);

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Get, Route, company.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-07", error.GetProperty("code").GetString());
        Assert.Equal($"A operação exige a Empresa aprovada. Estado atual da Empresa: {label}.", error.GetProperty("message").GetString());
    }

    /// <summary>
    /// O Administrador recebe 403 ERR-06.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AdminGetsErr06()
    {
        string token = await SeedAdminTokenAsync(this.factory);

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Get, Route, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-06", error.GetProperty("code").GetString());
        Assert.Equal(Err06Message, error.GetProperty("message").GetString());
    }

    /// <summary>
    /// Sem credencial a resposta é 401 ERR-01.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WithoutCredentialGetsErr01()
    {
        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Get, Route, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-01", error.GetProperty("code").GetString());
        Assert.Equal(InvalidSessionMessage, error.GetProperty("message").GetString());
    }

    private string UrlOf(string path)
    {
        return this.factory.Services.GetRequiredService<IFileUrlBuilder>().ToUrl(path)!;
    }

    private async Task<List<Guid>> ListIdsAsync(string token)
    {
        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Get, Route, token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await AuthTestAccounts.ReadJsonAsync(response))
            .EnumerateArray()
            .Select(item => item.GetProperty("matchId").GetGuid())
            .ToList();
    }
}
