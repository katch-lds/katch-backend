// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes das regras sobre contas bloqueadas ou suspensas: o Candidato sai da lista de espera e não pode ser avaliado
/// (FUN-005), e as vagas do Recrutador bloqueado deixam de ser visíveis aos Candidatos (FUN-006). Ao reativar a conta,
/// tudo volta. Cada teste usa uma aplicação própria, para que a exploração de vagas só veja as vagas do teste.
/// </summary>
public sealed class InactiveAccountRulesTests : IDisposable
{
    private readonly KatchWebApplicationFactory factory = new();

    /// <inheritdoc/>
    public void Dispose()
    {
        this.factory.Dispose();
    }

    /// <summary>
    /// Um Candidato bloqueado ou suspenso sai da lista de espera e da contagem da vaga, e abrir o perfil dá 404 ERR-20;
    /// reativado, volta a aparecer.
    /// </summary>
    /// <param name="suspended"><see langword="true"/> para suspender a conta; <see langword="false"/> para a bloquear.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InactiveCandidateLeavesTheWaitingListUntilReactivated(bool suspended)
    {
        (Guid companyId, _, string recruiterToken) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        Match match = await CandidateEvaluationTestData.SeedMatchAsync(this.factory, candidate.Id, job.Id);

        await this.ChangeAccountAsync(candidate.Id, account =>
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
        JsonElement hidden = await this.GetJsonAsync($"/api/recruiter/jobs/{job.Id}/waiting-candidates", recruiterToken);
        int hiddenCount = await this.WaitingCountAsync(job.Id, recruiterToken);
        using HttpResponseMessage profile = await this.SendAsync(
            HttpMethod.Get,
            $"/api/recruiter/interests/{match.Id}/profile",
            recruiterToken);

        await this.ChangeAccountAsync(candidate.Id, account => account.Reactivate(DateTimeOffset.UtcNow));
        JsonElement visible = await this.GetJsonAsync($"/api/recruiter/jobs/{job.Id}/waiting-candidates", recruiterToken);

        Assert.Equal(0, hidden.GetArrayLength());
        Assert.Equal(0, hiddenCount);
        Assert.Equal(HttpStatusCode.NotFound, profile.StatusCode);
        Assert.Equal("ERR-20", (await AuthTestAccounts.ReadJsonAsync(profile)).GetProperty("code").GetString());
        Assert.Equal(match.Id, Assert.Single(visible.EnumerateArray()).GetProperty("matchId").GetGuid());
        Assert.Equal(1, await this.WaitingCountAsync(job.Id, recruiterToken));
    }

    /// <summary>
    /// Com o Recrutador bloqueado, a vaga sai dos cartões, o detalhe e a página da Empresa dão 404 e o interesse dá
    /// ERR-33, sem gastar quota; reativada a conta, a vaga volta a aparecer.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task JobsOfABlockedRecruiterAreHiddenUntilReactivated()
    {
        (Guid companyId, Guid recruiterId, _) = await CandidateEvaluationTestData.SeedRecruiterAsync(this.factory);
        Job job = await CandidateEvaluationTestData.SeedJobAsync(this.factory, companyId);
        (Candidate candidate, _) = await CandidateEvaluationTestData.SeedCandidateAsync(this.factory);
        string candidateToken = this.factory.Services.GetRequiredService<ITokenService>().CreateToken(candidate.User).Token;

        using HttpResponseMessage cardBefore = await this.SendAsync(HttpMethod.Get, "/api/candidate/jobs/next", candidateToken);
        await this.ChangeAccountAsync(recruiterId, account => account.Block(DateTimeOffset.UtcNow));
        using HttpResponseMessage card = await this.SendAsync(HttpMethod.Get, "/api/candidate/jobs/next", candidateToken);
        using HttpResponseMessage detail = await this.SendAsync(HttpMethod.Get, $"/api/candidate/jobs/{job.Id}", candidateToken);
        using HttpResponseMessage page = await this.SendAsync(
            HttpMethod.Get,
            $"/api/candidate/jobs/companies/{companyId}",
            candidateToken);
        using HttpResponseMessage interest = await this.SendAsync(
            HttpMethod.Post,
            $"/api/candidate/jobs/{job.Id}/interest",
            candidateToken);
        int quotaWhileBlocked = await this.ReadQuotaAsync(candidate.Id);
        await this.ChangeAccountAsync(recruiterId, account => account.Reactivate(DateTimeOffset.UtcNow));
        using HttpResponseMessage cardAfter = await this.SendAsync(HttpMethod.Get, "/api/candidate/jobs/next", candidateToken);

        Assert.Equal(HttpStatusCode.OK, cardBefore.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, card.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, interest.StatusCode);
        Assert.Equal("ERR-33", (await AuthTestAccounts.ReadJsonAsync(interest)).GetProperty("code").GetString());
        Assert.Equal(Candidate.MaxInterests, quotaWhileBlocked);
        Assert.Equal(HttpStatusCode.OK, cardAfter.StatusCode);
        Assert.Equal(job.Id, (await AuthTestAccounts.ReadJsonAsync(cardAfter)).GetProperty("jobId").GetGuid());
    }

    private async Task ChangeAccountAsync(Guid userId, Action<AppUser> change)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        AppUser account = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
        change(account);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> ReadQuotaAsync(Guid candidateId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<KatchDbContext>().Candidates
            .AsNoTracking()
            .Where(candidate => candidate.Id == candidateId)
            .Select(candidate => candidate.SwipeRightsRemaining)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> WaitingCountAsync(Guid jobId, string token)
    {
        JsonElement jobs = await this.GetJsonAsync("/api/recruiter/jobs", token);
        return jobs.EnumerateArray()
            .Single(item => item.GetProperty("jobId").GetGuid() == jobId)
            .GetProperty("waitingCandidates")
            .GetInt32();
    }

    private async Task<JsonElement> GetJsonAsync(string path, string token)
    {
        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, path, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await AuthTestAccounts.ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
