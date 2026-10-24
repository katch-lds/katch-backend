// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de persistência de <see cref="Match"/> sobre SQLite in-memory: gravação e leitura de cada estado, unicidade
/// Candidato–vaga e chaves estrangeiras. Os <c>CHECK</c>, os tipos enumerados e o <c>xmin</c> são específicos do
/// PostgreSQL e estão verificados nos testes unitários do mapeamento e na evidência da migration.
/// </summary>
public sealed class MatchPersistenceTests : IClassFixture<KatchWebApplicationFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);

    private static int taxIdCounter;

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchPersistenceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public MatchPersistenceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// O interesse em espera é gravado com o Candidato, a vaga e a data do interesse, sem decisão nem conversa, e lido de
    /// volta igual.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WaitingInterestIsSavedAndReadBack()
    {
        (Candidate candidate, Job job, _) = await this.SeedAsync("espera");
        var match = Match.ExpressInterest(candidate.Id, job.Id, Now);
        await this.SaveAsync(match);

        Match saved = await this.ReadAsync(match.Id);

        Assert.Equal(candidate.Id, saved.CandidateId);
        Assert.Equal(job.Id, saved.JobId);
        Assert.Equal(MatchStatus.Waiting, saved.Status);
        Assert.Equal(Now, saved.CandidateActionAt);
        Assert.Null(saved.ProfileOpenedAt);
        Assert.Null(saved.RecruiterActionAt);
        Assert.Null(saved.DecidedById);
        Assert.Null(saved.ConversationStatus);
        Assert.Null(saved.ConversationClosedAt);
        Assert.Null(saved.ConversationClosedById);
        Assert.Null(saved.ConversationCloseReason);
    }

    /// <summary>
    /// A recusa da vaga pelo Candidato é gravada no estado <see cref="MatchStatus.DeclinedByCandidate"/>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DeclinedJobIsSavedAndReadBack()
    {
        (Candidate candidate, Job job, _) = await this.SeedAsync("recusa");
        var match = Match.Decline(candidate.Id, job.Id, Now);
        await this.SaveAsync(match);

        Match saved = await this.ReadAsync(match.Id);

        Assert.Equal(MatchStatus.DeclinedByCandidate, saved.Status);
        Assert.False(saved.IsInterest());
        Assert.Equal(Now, saved.CandidateActionAt);
    }

    /// <summary>
    /// O percurso completo do match — abertura do perfil, aceitação e encerramento da conversa por uma das partes — é
    /// gravado e lido com todas as datas, autores, estado e motivo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConfirmedMatchWithAClosedConversationIsSavedAndReadBack()
    {
        (Candidate candidate, Job job, Recruiter recruiter) = await this.SeedAsync("match");
        var match = Match.ExpressInterest(candidate.Id, job.Id, Now);
        match.OpenProfile(Now.AddMinutes(2));
        match.Accept(recruiter.Id, Now.AddMinutes(5));
        match.CloseConversation(candidate.Id, ConversationCloseReason.ClosedByParty, Now.AddDays(1));
        await this.SaveAsync(match);

        Match saved = await this.ReadAsync(match.Id);

        Assert.Equal(MatchStatus.Matched, saved.Status);
        Assert.Equal(Now.AddMinutes(2), saved.ProfileOpenedAt);
        Assert.Equal(Now.AddMinutes(5), saved.RecruiterActionAt);
        Assert.Equal(recruiter.Id, saved.DecidedById);
        Assert.Equal(ConversationStatus.Closed, saved.ConversationStatus);
        Assert.Equal(Now.AddDays(1), saved.ConversationClosedAt);
        Assert.Equal(candidate.Id, saved.ConversationClosedById);
        Assert.Equal(ConversationCloseReason.ClosedByParty, saved.ConversationCloseReason);
        Assert.False(saved.CanSendMessages());
    }

    /// <summary>
    /// A conversa aberta de um match confirmado e a recusa do Candidato pelo Recrutador são gravadas e lidas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenConversationAndRejectedCandidateAreSavedAndReadBack()
    {
        (Candidate candidate, Job job, Recruiter recruiter) = await this.SeedAsync("aberta");
        (Candidate other, _, _) = await this.SeedAsync("recusado");
        var accepted = Match.ExpressInterest(candidate.Id, job.Id, Now);
        accepted.OpenProfile(Now);
        accepted.Accept(recruiter.Id, Now.AddMinutes(5));
        var rejected = Match.ExpressInterest(other.Id, job.Id, Now);
        rejected.OpenProfile(Now);
        rejected.Reject(recruiter.Id, Now.AddMinutes(6));
        await this.SaveAsync(accepted);
        await this.SaveAsync(rejected);

        Match savedAccepted = await this.ReadAsync(accepted.Id);
        Match savedRejected = await this.ReadAsync(rejected.Id);

        Assert.Equal(ConversationStatus.Open, savedAccepted.ConversationStatus);
        Assert.True(savedAccepted.CanSendMessages());
        Assert.Equal(MatchStatus.RejectedByRecruiter, savedRejected.Status);
        Assert.Equal(Now.AddMinutes(6), savedRejected.RecruiterActionAt);
        Assert.Null(savedRejected.ConversationStatus);
    }

    /// <summary>
    /// Uma segunda resposta do mesmo Candidato à mesma vaga é rejeitada pela unicidade <c>uq_match_candidate_job</c>
    /// (RF019).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SecondAnswerOfTheSameCandidateToTheSameJobIsRejected()
    {
        (Candidate candidate, Job job, _) = await this.SeedAsync("duplicada");
        await this.SaveAsync(Match.ExpressInterest(candidate.Id, job.Id, Now));

        await Assert.ThrowsAsync<DbUpdateException>(() => this.SaveAsync(Match.Decline(candidate.Id, job.Id, Now)));
    }

    /// <summary>
    /// Candidato, vaga, Recrutador ou conta de encerramento inexistentes são rejeitados pelas chaves estrangeiras.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownReferencesAreRejectedByTheForeignKeys()
    {
        (Candidate candidate, Job job, Recruiter recruiter) = await this.SeedAsync("fk");

        await Assert.ThrowsAsync<DbUpdateException>(() => this.SaveAsync(Match.ExpressInterest(Guid.NewGuid(), job.Id, Now)));
        await Assert.ThrowsAsync<DbUpdateException>(() => this.SaveAsync(Match.ExpressInterest(candidate.Id, Guid.NewGuid(), Now)));

        var unknownRecruiter = Match.ExpressInterest(candidate.Id, job.Id, Now);
        unknownRecruiter.OpenProfile(Now);
        unknownRecruiter.Accept(Guid.NewGuid(), Now);
        await Assert.ThrowsAsync<DbUpdateException>(() => this.SaveAsync(unknownRecruiter));

        var unknownAuthor = Match.ExpressInterest(candidate.Id, job.Id, Now);
        unknownAuthor.OpenProfile(Now);
        unknownAuthor.Accept(recruiter.Id, Now);
        unknownAuthor.CloseConversation(Guid.NewGuid(), ConversationCloseReason.ClosedByParty, Now);
        await Assert.ThrowsAsync<DbUpdateException>(() => this.SaveAsync(unknownAuthor));
    }

    /// <summary>
    /// As respostas a uma vaga eliminada continuam nas consultas: o filtro global das vagas eliminadas (DM-09) não se
    /// estende à tabela <c>match</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AnswersToADeletedJobAreStillReadable()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (Candidate candidate, Job job, _) = await this.SeedAsync("eliminada");
        var match = Match.Decline(candidate.Id, job.Id, Now);
        await this.SaveAsync(match);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Job stored = await context.Jobs.SingleAsync(item => item.Id == job.Id, cancellationToken);
            stored.MarkDeleted(Now);
            await context.SaveChangesAsync(cancellationToken);
        }

        Match saved = await this.ReadAsync(match.Id);

        Assert.Equal(job.Id, saved.JobId);
        Assert.Equal(MatchStatus.DeclinedByCandidate, saved.Status);
    }

    private async Task<(Candidate Candidate, Job Job, Recruiter Recruiter)> SeedAsync(string name)
    {
        var recruiterUser = new AppUser($"recrutador-{name}@katch.pt", "hash", UserType.Recruiter, Now, Now);
        var recruiter = new Recruiter(recruiterUser);
        var location = new Location($"Braga {name}", "Braga", 41.545450m, -8.426507m);
        var company = new Company(
            recruiter.Id,
            new CompanyRegistration
            {
                CompanyName = "Lumen Software, Lda.",
                TaxId = (509_500_000 + Interlocked.Increment(ref taxIdCounter)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Industry = Industry.Technology,
                Address = "Avenida da Liberdade, 100, 4710-000 Braga",
                LocationId = location.Id,
                ContactEmail = "geral@lumen-exemplo.pt",
                ContactPhone = "253000111",
                ResponsibleName = "Rui Matos",
            },
            Now);
        company.Approve();
        var job = new Job(
            company.Id,
            new JobData
            {
                Title = "Programador Backend .NET",
                Description = "Desenvolvimento de APIs REST em ASP.NET Core.",
                MinSalary = 1600m,
                MaxSalary = 2100m,
                LocationId = location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        var candidateUser = new AppUser($"candidato-{name}@katch.pt", "hash", UserType.Candidate, Now, Now);
        var candidate = new Candidate(candidateUser, "Ana Ferreira", "912345678", location.Id);

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        context.Recruiters.Add(recruiter);
        context.Locations.Add(location);
        context.Companies.Add(company);
        context.Jobs.Add(job);
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (candidate, job, recruiter);
    }

    private async Task SaveAsync(Match match)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        context.Matches.Add(match);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Match> ReadAsync(Guid matchId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Matches
            .AsNoTracking()
            .SingleAsync(item => item.Id == matchId, TestContext.Current.CancellationToken);
    }
}
