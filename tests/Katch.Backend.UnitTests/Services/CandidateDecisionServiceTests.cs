// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes da decisão do Recrutador em <see cref="CandidateEvaluationService"/>: aceitar e recusar (RF063 a RF066, RF068,
/// RF117; documentação da API v02, secções 6.6.4 e 6.6.5), com os repositórios simulados. A ordem das verificações (ERR-20,
/// ERR-08, ERR-35, ERR-34), a atomicidade e a perda na concorrência (<c>xmin</c>) só se exercitam aqui, porque o SQLite
/// dos testes de integração não tem a coluna de versão.
/// </summary>
public sealed class CandidateDecisionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 15, 0, 0, TimeSpan.Zero);

    private readonly Guid recruiterId = Guid.NewGuid();
    private readonly Location location = new("Braga", "Braga", 41.545m, -8.426m);
    private readonly Company company;
    private readonly Job job;
    private readonly Candidate candidate;
    private readonly AppUser candidateAccount;
    private readonly Store store;
    private readonly FakeUserRepository users = new();
    private readonly FixedClock clock = new() { UtcNow = Now };
    private readonly CandidateEvaluationService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateDecisionServiceTests"/> class.
    /// </summary>
    public CandidateDecisionServiceTests()
    {
        this.company = NewCompany(this.recruiterId);
        this.company.SetLogo("company_logo/empresa.png");
        this.job = NewJob(this.company.Id, this.location.Id);

        this.candidateAccount = new AppUser("ana.ferreira@exemplo.pt", "hash", UserType.Candidate, Now.AddYears(-1), Now.AddYears(-1));
        this.candidate = new Candidate(this.candidateAccount, "Ana Ferreira", "912345678", this.location.Id);
        this.users.Add(this.candidateAccount);

        this.store = new Store(this.candidate, this.location, this.company);
        this.store.Jobs[this.job.Id] = this.job;
        this.service = new CandidateEvaluationService(
            this.store,
            this.store,
            this.store,
            this.store,
            this.users,
            this.store,
            this.store,
            this.store,
            new FileUrlBuilder(),
            this.clock);
    }

    /// <summary>
    /// A aceitação confirma o match com o Recrutador e a data do relógio do servidor, abre a conversa e grava uma só vez
    /// (RF065, RF066, RF068).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AcceptConfirmsTheMatchAndOpensTheConversationInOneSave()
    {
        Match match = this.AddOpenedInterest();

        await this.service.AcceptAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Equal(MatchStatus.Matched, match.Status);
        Assert.Equal(this.recruiterId, match.DecidedById);
        Assert.Equal(Now, match.RecruiterActionAt);
        Assert.Equal(ConversationStatus.Open, match.ConversationStatus);
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// A resposta traz o match com a vaga, a Empresa e o contacto do Candidato; <c>matchedAt</c> é o instante da decisão.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AcceptReturnsTheMatchWithTheCandidateContact()
    {
        Match match = this.AddOpenedInterest();

        MatchDto dto = await this.service.AcceptAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Equal(match.Id, dto.MatchId);
        Assert.Equal("Programador", dto.JobTitle);
        Assert.Equal(JobStatus.Published, dto.JobStatus);
        Assert.Equal("Braga (Braga)", dto.LocationName);
        Assert.Equal("Empresa de teste", dto.CompanyName);
        Assert.Equal("/api/files/COMPANY_LOGO/empresa.png", dto.LogoUrl);
        Assert.Equal("Ana Ferreira", dto.CounterpartName);
        Assert.Equal("ana.ferreira@exemplo.pt", dto.ContactEmail);
        Assert.Equal("912345678", dto.ContactPhone);
        Assert.Equal(Now, dto.MatchedAt);
    }

    /// <summary>
    /// A recusa regista o Recrutador e a data, não abre conversa e grava uma só vez (RF064, RF065).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectRecordsTheDecisionWithoutConversation()
    {
        Match match = this.AddOpenedInterest();

        await this.service.RejectAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Equal(MatchStatus.RejectedByRecruiter, match.Status);
        Assert.Equal(this.recruiterId, match.DecidedById);
        Assert.Equal(Now, match.RecruiterActionAt);
        Assert.Null(match.ConversationStatus);
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// Sem a abertura do perfil registada, a decisão dá ERR-34 e nada é alterado nem gravado (RF063, RF064).
    /// </summary>
    /// <param name="accept"><see langword="true"/> para aceitar e <see langword="false"/> para recusar.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DecisionWithoutOpenedProfileReturnsErr34AndChangesNothing(bool accept)
    {
        Match match = this.AddInterest();

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(() => this.DecideAsync(accept, match.Id));

        Assert.Equal("ERR-34", exception.Error.Code);
        Assert.Equal("Abra o perfil completo do Candidato antes de registar a decisão.", exception.Error.Message);
        AssertUndecided(match);
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// Uma decisão já registada não se altera: aceitar ou recusar de novo, tanto no mesmo sentido como no contrário, dá
    /// ERR-35 e mantém o primeiro registo (RF117).
    /// </summary>
    /// <param name="firstAccepts">Sentido da primeira decisão.</param>
    /// <param name="secondAccepts">Sentido da segunda decisão.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task ARegisteredDecisionIsNeverChanged(bool firstAccepts, bool secondAccepts)
    {
        Match match = this.AddOpenedInterest();
        await this.DecideAsync(firstAccepts, match.Id);
        this.clock.UtcNow = Now.AddHours(2);
        MatchStatus decidedStatus = match.Status;

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(() => this.DecideAsync(secondAccepts, match.Id));

        Assert.Equal("ERR-35", exception.Error.Code);
        Assert.Equal("O Candidato já foi avaliado nesta vaga.", exception.Error.Message);
        Assert.Equal(decidedStatus, match.Status);
        Assert.Equal(Now, match.RecruiterActionAt);
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// A decisão sobre um interesse que já não está em espera tem o ERR-35 antes do ERR-34, como a abertura do perfil.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ADecidedInterestReturnsErr35EvenWithoutAnOpeningRecord()
    {
        Match match = this.AddOpenedInterest();
        match.Reject(this.recruiterId, Now.AddDays(-1));

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.AcceptAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-35", exception.Error.Code);
    }

    /// <summary>
    /// Um interesse inexistente, ou a recusa da vaga pelo Candidato, que não é um interesse, dá ERR-20.
    /// </summary>
    /// <param name="accept"><see langword="true"/> para aceitar e <see langword="false"/> para recusar.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DecisionOnAnUnknownInterestOrADeclinedJobReturnsErr20(bool accept)
    {
        Match declined = Match.Decline(this.candidate.Id, this.job.Id, Now.AddDays(-1));
        this.store.Matches[declined.Id] = declined;

        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.DecideAsync(accept, Guid.NewGuid()));
        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.DecideAsync(accept, declined.Id));
        Assert.Equal(MatchStatus.DeclinedByCandidate, declined.Status);
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// Só o Recrutador da Empresa da vaga decide: o de outra Empresa, ou sem Empresa, recebe ERR-08 e nada é alterado.
    /// </summary>
    /// <param name="accept"><see langword="true"/> para aceitar e <see langword="false"/> para recusar.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyTheRecruiterOfTheJobCompanyDecides(bool accept)
    {
        Match match = this.AddOpenedInterest();
        Job foreignJob = NewJob(Guid.NewGuid(), this.location.Id);
        this.store.Jobs[foreignJob.Id] = foreignJob;
        Match foreign = this.AddOpenedInterest(foreignJob.Id);

        await Assert.ThrowsAsync<ElementForbiddenException>(() => this.DecideAsync(accept, foreign.Id));
        await Assert.ThrowsAsync<ElementForbiddenException>(
            () => this.DecideAsync(accept, match.Id, recruiter: Guid.NewGuid()));

        AssertUndecided(foreign);
        AssertUndecided(match);
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// A titularidade (ERR-08) é verificada antes do estado: um Recrutador alheio não descobre se o interesse já foi
    /// decidido nem se o perfil foi aberto.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OwnershipIsCheckedBeforeTheStateOfTheInterest()
    {
        Job foreignJob = NewJob(Guid.NewGuid(), this.location.Id);
        this.store.Jobs[foreignJob.Id] = foreignJob;
        Match unopened = this.AddInterest(foreignJob.Id);
        Match decided = this.AddOpenedInterest(foreignJob.Id);
        decided.Accept(Guid.NewGuid(), Now.AddDays(-1));

        await Assert.ThrowsAsync<ElementForbiddenException>(() => this.DecideAsync(accept: true, unopened.Id));
        await Assert.ThrowsAsync<ElementForbiddenException>(() => this.DecideAsync(accept: false, decided.Id));
    }

    /// <summary>
    /// Se outro pedido de decisão ganhou a corrida (<c>xmin</c>), a gravação falha, as alterações por gravar são
    /// descartadas e a resposta é o ERR-35, sem repetir o pedido: a decisão perdida nunca se sobrepõe à primeira.
    /// </summary>
    /// <param name="accept"><see langword="true"/> para aceitar e <see langword="false"/> para recusar.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LosingTheRaceReturnsErr35AndDiscardsTheChanges(bool accept)
    {
        Match match = this.AddOpenedInterest();
        this.store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(() => this.DecideAsync(accept, match.Id));

        Assert.Equal("ERR-35", exception.Error.Code);
        Assert.Equal(1, this.store.Discards);
        Assert.Equal(1, this.store.SaveAttempts);
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// Os dados da resposta são lidos antes de gravar: se faltarem, o pedido falha sem registar a aceitação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AcceptReadsEverythingBeforeSavingSoAFailureLeavesNothingRegistered()
    {
        Match match = this.AddOpenedInterest();
        var missingAccounts = new FakeUserRepository();
        var serviceWithoutAccount = new CandidateEvaluationService(
            this.store,
            this.store,
            this.store,
            this.store,
            missingAccounts,
            this.store,
            this.store,
            this.store,
            new FileUrlBuilder(),
            this.clock);

        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => serviceWithoutAccount.AcceptAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken));

        AssertUndecided(match);
        Assert.Equal(0, this.store.SaveAttempts);
    }

    /// <summary>
    /// A decisão usa sempre o relógio do servidor e o Recrutador da credencial.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TheDecisionUsesTheServerClock()
    {
        Match match = this.AddOpenedInterest();
        this.clock.UtcNow = Now.AddMinutes(7);

        MatchDto dto = await this.service.AcceptAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Equal(Now.AddMinutes(7), match.RecruiterActionAt);
        Assert.Equal(Now.AddMinutes(7), dto.MatchedAt);
    }

    private static void AssertUndecided(Match match)
    {
        Assert.Equal(MatchStatus.Waiting, match.Status);
        Assert.Null(match.DecidedById);
        Assert.Null(match.RecruiterActionAt);
        Assert.Null(match.ConversationStatus);
    }

    private static Company NewCompany(Guid recruiterId)
    {
        var company = new Company(
            recruiterId,
            new CompanyRegistration
            {
                CompanyName = "Empresa de teste",
                TaxId = "500000000",
                Industry = Industry.Technology,
                Address = "Rua de teste, 1",
                LocationId = Guid.NewGuid(),
                ContactEmail = "teste@exemplo.pt",
                ContactPhone = "253000000",
                ResponsibleName = "Responsável",
            },
            Now.AddMonths(-1));
        company.Approve();
        return company;
    }

    private static Job NewJob(Guid companyId, Guid locationId)
    {
        var job = new Job(
            companyId,
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
        job.Publish(Now.AddDays(-10));
        return job;
    }

    private Task DecideAsync(bool accept, Guid matchId, Guid? recruiter = null)
    {
        Guid actingRecruiter = recruiter ?? this.recruiterId;
        return accept
            ? this.service.AcceptAsync(actingRecruiter, matchId, TestContext.Current.CancellationToken)
            : this.service.RejectAsync(actingRecruiter, matchId, TestContext.Current.CancellationToken);
    }

    private Match AddInterest(Guid? jobId = null)
    {
        Match match = Match.ExpressInterest(this.candidate.Id, jobId ?? this.job.Id, Now.AddDays(-1));
        this.store.Matches[match.Id] = match;
        return match;
    }

    private Match AddOpenedInterest(Guid? jobId = null)
    {
        Match match = this.AddInterest(jobId);
        match.OpenProfile(Now.AddHours(-1));
        return match;
    }

    private sealed class Store : IMatchRepository, IJobRepository, ICompanyRepository, ICandidateRepository, IReferenceDataRepository, IUnitOfWork, IFileStorage
    {
        private readonly Candidate candidate;
        private readonly Location location;
        private readonly Company company;

        public Store(Candidate candidate, Location location, Company company)
        {
            this.candidate = candidate;
            this.location = location;
            this.company = company;
        }

        public Dictionary<Guid, Match> Matches { get; } = [];

        public Dictionary<Guid, Job> Jobs { get; } = [];

        public Queue<Exception> FailNextSaves { get; } = new();

        public int Saves { get; private set; }

        public int SaveAttempts { get; private set; }

        public int Discards { get; private set; }

        public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Matches.GetValueOrDefault(id));
        }

        public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        Task IMatchRepository.AddAsync(Match match, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<List<WaitingInterest>> ListWaitingByJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> HasInterestFromCandidateInCompanyAsync(Guid candidateId, Guid recruiterId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Job?> GetWithSkillsAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Jobs.GetValueOrDefault(jobId));
        }

        public Task<JobWithCompanyStatus?> GetWithCompanyStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(recruiterId == this.company.RecruiterId ? this.company : null);
        }

        public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        Task ICompanyRepository.AddAsync(Company company, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        Task ICandidateRepository.AddAsync(Candidate candidate, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        Task<Candidate?> ICandidateRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(id == this.candidate.Id ? this.candidate : null);
        }

        public Task<Candidate?> GetWithProfileAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Candidate>> ListQuotaDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Candidate?> GetByProfilePhotoPathAsync(string path, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Location?> GetLocationAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == this.location.Id ? this.location : null);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            this.SaveAttempts++;
            if (this.FailNextSaves.TryDequeue(out Exception? failure))
            {
                throw failure;
            }

            this.Saves++;
            return Task.FromResult(1);
        }

        public void DiscardChanges()
        {
            this.Discards++;
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<string> SaveAsync(Stream content, FileKind kind, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
