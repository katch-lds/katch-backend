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
/// Testes de <see cref="CandidateEvaluationService"/>: lista de candidatos em espera, abertura do perfil completo e
/// obtenção do curriculum vitae (RF060 a RF062, RF113, RF118), com os repositórios simulados. A ordem das verificações
/// (ERR-20, ERR-08, ERR-35) e a repetição perante conflitos de concorrência (<c>xmin</c>) só se exercitam aqui, porque o
/// SQLite dos testes de integração não tem a coluna de versão.
/// </summary>
public sealed class CandidateEvaluationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];

    private readonly Guid recruiterId = Guid.NewGuid();
    private readonly Location location = new("Guimarães", "Braga", 41.445m, -8.292m);
    private readonly Skill java = new("Java");
    private readonly Skill sql = new("SQL");
    private readonly Skill python = new("Python");
    private readonly Company company;
    private readonly Job job;
    private readonly Candidate candidate;
    private readonly Store store;
    private readonly FakeMatches matches = new();
    private readonly FixedClock clock = new() { UtcNow = Now };
    private readonly CandidateEvaluationService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateEvaluationServiceTests"/> class.
    /// </summary>
    public CandidateEvaluationServiceTests()
    {
        this.company = NewCompany(this.recruiterId);
        this.job = NewJob(this.company.Id);
        this.job.Skills.Add(this.java);
        this.job.Skills.Add(this.sql);

        var user = new AppUser("ana@exemplo.pt", "hash", UserType.Candidate, Now.AddYears(-1), Now.AddYears(-1));
        this.candidate = new Candidate(user, "Ana Ferreira", "912345678", this.location.Id)
        {
            DesiredRole = "Programadora",
            Availability = Availability.Immediate,
            ExperienceSummary = "Cinco anos em backend.",
        };

        this.store = new Store(this.candidate, this.location, this.company);
        this.store.Jobs[this.job.Id] = this.job;
        this.service = new CandidateEvaluationService(
            this.matches,
            this.store,
            this.store,
            this.store,
            new FakeUserRepository(),
            this.store,
            this.store,
            this.store,
            new FileUrlBuilder(),
            this.clock);
    }

    /// <summary>
    /// A lista devolve os candidatos em espera pela ordem do repositório, com a localidade e o distrito.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListWaitingMapsEachInterestWithTheLocationAndDistrict()
    {
        var first = new WaitingInterest(Guid.NewGuid(), Guid.NewGuid(), "Ana Ferreira", "Programadora", this.location, Now.AddDays(-2));
        var second = new WaitingInterest(Guid.NewGuid(), Guid.NewGuid(), "Bruno Costa", null, this.location, Now.AddDays(-1));
        this.matches.Waiting[this.job.Id] = [first, second];

        List<WaitingCandidateDto> waiting = await this.service.ListWaitingAsync(this.recruiterId, this.job.Id, TestContext.Current.CancellationToken);

        Assert.Equal([first.MatchId, second.MatchId], waiting.Select(item => item.MatchId));
        Assert.Equal("Guimarães (Braga)", waiting[0].LocationName);
        Assert.Equal(first.CandidateId, waiting[0].CandidateId);
        Assert.Equal("Ana Ferreira", waiting[0].FullName);
        Assert.Equal("Programadora", waiting[0].DesiredRole);
        Assert.Equal(first.InterestAt, waiting[0].InterestAt);
        Assert.Null(waiting[1].DesiredRole);
    }

    /// <summary>
    /// Uma vaga sem candidatos em espera devolve a lista vazia, não um erro.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListWaitingReturnsAnEmptyListWhenNobodyIsWaiting()
    {
        List<WaitingCandidateDto> waiting = await this.service.ListWaitingAsync(this.recruiterId, this.job.Id, TestContext.Current.CancellationToken);

        Assert.Empty(waiting);
    }

    /// <summary>
    /// Uma vaga inexistente ou eliminada dá ERR-20.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListWaitingRejectsAnUnknownJob()
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.ListWaitingAsync(this.recruiterId, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Uma vaga de outra Empresa dá ERR-08, sem revelar os candidatos.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListWaitingRejectsAJobOfAnotherCompany()
    {
        Job other = NewJob(Guid.NewGuid());
        this.store.Jobs[other.Id] = other;

        await Assert.ThrowsAsync<ElementForbiddenException>(
            () => this.service.ListWaitingAsync(this.recruiterId, other.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, this.matches.WaitingLoads);
    }

    /// <summary>
    /// Um Recrutador sem Empresa registada não tem vagas próprias (ERR-08).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListWaitingRejectsARecruiterWithoutCompany()
    {
        await Assert.ThrowsAsync<ElementForbiddenException>(
            () => this.service.ListWaitingAsync(Guid.NewGuid(), this.job.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A primeira abertura regista a data e hora do relógio do servidor e grava uma vez (RF118).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileRecordsTheFirstOpening()
    {
        Match match = this.AddInterest();

        CandidateFullProfileDto profile = await this.service.OpenProfileAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Equal(Now, profile.ProfileOpenedAt);
        Assert.Equal(Now, match.ProfileOpenedAt);
        Assert.Equal(match.Id, profile.MatchId);
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// As aberturas seguintes devolvem a data da primeira e não escrevem na base de dados.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileAgainKeepsTheFirstOpeningAndDoesNotSave()
    {
        Match match = this.AddInterest();
        await this.service.OpenProfileAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);
        this.clock.UtcNow = Now.AddHours(3);

        CandidateFullProfileDto profile = await this.service.OpenProfileAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Equal(Now, profile.ProfileOpenedAt);
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// O registo da abertura é por vaga: abrir o perfil do mesmo Candidato noutra vaga não altera o primeiro interesse.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileRecordsTheOpeningPerJob()
    {
        Job otherJob = NewJob(this.company.Id);
        this.store.Jobs[otherJob.Id] = otherJob;
        Match first = this.AddInterest();
        Match second = this.AddInterest(otherJob.Id);

        await this.service.OpenProfileAsync(this.recruiterId, first.Id, TestContext.Current.CancellationToken);

        Assert.Equal(Now, first.ProfileOpenedAt);
        Assert.Null(second.ProfileOpenedAt);
    }

    /// <summary>
    /// Só as competências da lista que a vaga pede ficam destacadas; as da opção «Outro» nunca o ficam. A ordem é
    /// alfabética (RF062).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileHighlightsOnlyTheSkillsTheJobRequires()
    {
        this.candidate.AddSkill(this.sql);
        this.candidate.AddSkill(this.python);
        this.candidate.AddCustomSkill("Java");
        Match match = this.AddInterest();

        CandidateFullProfileDto profile = await this.service.OpenProfileAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            [new SkillTagDto("Java", false), new SkillTagDto("Python", false), new SkillTagDto("SQL", true)],
            profile.Skills);
    }

    /// <summary>
    /// O perfil traz os campos do Candidato, o URL da fotografia e <c>hasCv</c>, nunca os caminhos dos ficheiros.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileReturnsTheFullProfileWithUrlsAndNoPaths()
    {
        this.candidate.ProfilePhotoPath = "profile_photo/abc.png";
        this.candidate.CvPath = "curriculum_vitae/abc.pdf";
        this.candidate.SetLinks(["https://github.com/ana", "https://ana.pt"]);
        Match match = this.AddInterest();

        CandidateFullProfileDto profile = await this.service.OpenProfileAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Ana Ferreira", profile.FullName);
        Assert.Equal("/api/files/PROFILE_PHOTO/abc.png", profile.PhotoUrl);
        Assert.Equal("Programadora", profile.DesiredRole);
        Assert.Equal("Guimarães (Braga)", profile.LocationName);
        Assert.Equal(Availability.Immediate, profile.Availability);
        Assert.Equal("Cinco anos em backend.", profile.ExperienceSummary);
        Assert.Equal(["https://github.com/ana", "https://ana.pt"], profile.Links);
        Assert.True(profile.HasCv);
    }

    /// <summary>
    /// Sem fotografia nem curriculum vitae, <c>photoUrl</c> é nulo e <c>hasCv</c> é falso.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileWithoutFilesHasNoPhotoAndNoCv()
    {
        Match match = this.AddInterest();

        CandidateFullProfileDto profile = await this.service.OpenProfileAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Null(profile.PhotoUrl);
        Assert.False(profile.HasCv);
        Assert.Empty(profile.Links);
        Assert.Empty(profile.Skills);
    }

    /// <summary>
    /// Um interesse inexistente dá ERR-20.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileRejectsAnUnknownInterest()
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.OpenProfileAsync(this.recruiterId, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A recusa da vaga pelo Candidato não é um interesse: não há perfil a abrir (ERR-20).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileRejectsAProfileWithoutInterest()
    {
        Match declined = Match.Decline(this.candidate.Id, this.job.Id, Now.AddDays(-1));
        this.matches.Add(declined);

        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.OpenProfileAsync(this.recruiterId, declined.Id, TestContext.Current.CancellationToken));
        Assert.Null(declined.ProfileOpenedAt);
    }

    /// <summary>
    /// Um interesse numa vaga de outra Empresa dá ERR-08, mesmo que já não esteja em espera.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileRejectsAnInterestInAJobOfAnotherCompany()
    {
        Job other = NewJob(Guid.NewGuid());
        this.store.Jobs[other.Id] = other;
        Match waiting = this.AddInterest(other.Id);
        Match decided = this.AddInterest(other.Id);
        decided.OpenProfile(Now);
        decided.Reject(Guid.NewGuid(), Now);

        await Assert.ThrowsAsync<ElementForbiddenException>(
            () => this.service.OpenProfileAsync(this.recruiterId, waiting.Id, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ElementForbiddenException>(
            () => this.service.OpenProfileAsync(this.recruiterId, decided.Id, TestContext.Current.CancellationToken));
        Assert.Null(waiting.ProfileOpenedAt);
    }

    /// <summary>
    /// Um interesse já aceite ou recusado dá ERR-35 e não altera o registo.
    /// </summary>
    /// <param name="accept">Se a decisão registada foi a aceitação.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OpenProfileRejectsAnInterestAlreadyEvaluated(bool accept)
    {
        Match match = this.AddInterest();
        match.OpenProfile(Now.AddDays(-1));
        if (accept)
        {
            match.Accept(this.recruiterId, Now.AddDays(-1));
        }
        else
        {
            match.Reject(this.recruiterId, Now.AddDays(-1));
        }

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.OpenProfileAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-35", exception.Error.Code);
        Assert.Equal("O Candidato já foi avaliado nesta vaga.", exception.Error.Message);
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// Perante um conflito de concorrência, a abertura descarta as alterações, volta a ler e conclui.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileRetriesAfterAConcurrencyConflict()
    {
        Match match = this.AddInterest();
        this.store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        this.store.OnDiscard = () => this.matches.Replace(match.Id, Match.ExpressInterest(this.candidate.Id, this.job.Id, Now.AddDays(-1)));

        CandidateFullProfileDto profile = await this.service.OpenProfileAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Equal(Now, profile.ProfileOpenedAt);
        Assert.Equal(1, this.store.Discards);
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// Se a decisão foi registada por outro pedido entretanto, a repetição devolve ERR-35.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileReturnsAlreadyEvaluatedWhenTheDecisionWonTheRace()
    {
        Match match = this.AddInterest();
        this.store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        this.store.OnDiscard = () =>
        {
            Match decided = Match.ExpressInterest(this.candidate.Id, this.job.Id, Now.AddDays(-1));
            decided.OpenProfile(Now);
            decided.Reject(this.recruiterId, Now);
            this.matches.Replace(match.Id, decided);
        };

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.OpenProfileAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-35", exception.Error.Code);
    }

    /// <summary>
    /// Conflitos repetidos esgotam as tentativas e o conflito sobe (ERR-41), como último recurso.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenProfileGivesUpAfterTheMaximumAttempts()
    {
        Match match = this.AddInterest();
        this.store.FailAlways = new ConcurrencyConflictException(new InvalidOperationException());
        this.store.OnDiscard = () => this.matches.Replace(match.Id, Match.ExpressInterest(this.candidate.Id, this.job.Id, Now.AddDays(-1)));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => this.service.OpenProfileAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken));

        Assert.Equal(CandidateEvaluationService.MaxOpenProfileAttempts - 1, this.store.Discards);
    }

    /// <summary>
    /// O curriculum vitae é devolvido como PDF, com o nome <c>cv.pdf</c>, que não revela o caminho no servidor.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetCvReturnsThePdf()
    {
        this.candidate.CvPath = "curriculum_vitae/abc.pdf";
        this.store.Files["curriculum_vitae/abc.pdf"] = Pdf;
        Match match = this.AddInterest();

        FileContent cv = await this.service.GetCvAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        await using Stream content = cv.Content;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, TestContext.Current.CancellationToken);
        Assert.Equal(Pdf, copy.ToArray());
        Assert.Equal("application/pdf", cv.ContentType);
        Assert.Equal("cv.pdf", cv.FileName);
    }

    /// <summary>
    /// Um Candidato sem curriculum vitae, ou com o ficheiro em falta no armazenamento, dá ERR-20.
    /// </summary>
    /// <param name="storedPath">Caminho guardado no perfil, ou <see langword="null"/> se não houver curriculum vitae.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("curriculum_vitae/em-falta.pdf")]
    public async Task GetCvRejectsAMissingCv(string? storedPath)
    {
        this.candidate.CvPath = storedPath;
        Match match = this.AddInterest();

        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetCvAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Depois da decisão, o curriculum vitae deixa de estar disponível (ERR-35), e nunca o está para outra Empresa
    /// (ERR-08).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetCvIsOnlyAvailableWhileTheOwnInterestIsWaiting()
    {
        this.candidate.CvPath = "curriculum_vitae/abc.pdf";
        this.store.Files["curriculum_vitae/abc.pdf"] = Pdf;
        Match decided = this.AddInterest();
        decided.OpenProfile(Now);
        decided.Accept(this.recruiterId, Now);
        Job other = NewJob(Guid.NewGuid());
        this.store.Jobs[other.Id] = other;
        Match foreign = this.AddInterest(other.Id);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.GetCvAsync(this.recruiterId, decided.Id, TestContext.Current.CancellationToken));
        Assert.Equal("ERR-35", exception.Error.Code);
        await Assert.ThrowsAsync<ElementForbiddenException>(
            () => this.service.GetCvAsync(this.recruiterId, foreign.Id, TestContext.Current.CancellationToken));
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

    private static Job NewJob(Guid companyId)
    {
        var job = new Job(
            companyId,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = Guid.NewGuid(),
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        job.Publish(Now.AddDays(-10));
        return job;
    }

    private Match AddInterest(Guid? jobId = null)
    {
        Match match = Match.ExpressInterest(this.candidate.Id, jobId ?? this.job.Id, Now.AddDays(-1));
        this.matches.Add(match);
        return match;
    }

    private sealed class FakeMatches : IMatchRepository
    {
        private readonly Dictionary<Guid, Match> byId = [];

        public Dictionary<Guid, List<WaitingInterest>> Waiting { get; } = [];

        public int WaitingLoads { get; private set; }

        public void Add(Match match)
        {
            this.byId[match.Id] = match;
        }

        /// <summary>
        /// Simula a releitura depois de <c>DiscardChanges</c>: o identificador passa a devolver o novo estado da linha.
        /// </summary>
        /// <param name="id">Identificador pedido pelo serviço.</param>
        /// <param name="match">Novo estado do interesse.</param>
        public void Replace(Guid id, Match match)
        {
            this.byId[id] = match;
        }

        public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.byId.GetValueOrDefault(id));
        }

        public Task<List<WaitingInterest>> ListWaitingByJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            this.WaitingLoads++;
            return Task.FromResult(this.Waiting.GetValueOrDefault(jobId) ?? []);
        }

        public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Match match, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> HasInterestFromCandidateInCompanyAsync(Guid candidateId, Guid recruiterId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class Store : IJobRepository, ICompanyRepository, ICandidateRepository, IReferenceDataRepository, IUnitOfWork, IFileStorage
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

        public Dictionary<Guid, Job> Jobs { get; } = [];

        public Dictionary<string, byte[]> Files { get; } = [];

        public Queue<Exception> FailNextSaves { get; } = new();

        public Exception? FailAlways { get; set; }

        public Action? OnDiscard { get; set; }

        public int Saves { get; private set; }

        public int Discards { get; private set; }

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

        public Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == this.candidate.Id ? this.candidate : null);
        }

        public Task<Candidate?> GetWithProfileAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return this.GetByIdAsync(id, cancellationToken);
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
            if (this.FailNextSaves.TryDequeue(out Exception? failure))
            {
                throw failure;
            }

            if (this.FailAlways is not null)
            {
                throw this.FailAlways;
            }

            this.Saves++;
            return Task.FromResult(1);
        }

        public void DiscardChanges()
        {
            this.Discards++;
            this.OnDiscard?.Invoke();
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
            return this.Files.TryGetValue(path, out byte[]? bytes)
                ? Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))
                : throw new FileNotFoundException("Ficheiro inexistente.", path);
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
