// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes da suspensão e da reativação das Empresas e das listas globais em <see cref="CompanyAdminService"/>
/// (documentação da API v02, secções 6.11.5 a 6.11.8; RF075, RF095, RF097, RF098, RF112, RF116), com repositórios em
/// memória.
/// </summary>
public sealed class CompanyAdminServiceTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 9, 14, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.Parse("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c01");

    private static long taxIdCounter = 509_000_000;

    private readonly FakeCompanies companies = new();
    private readonly FakeJobs jobs = new();
    private readonly FakeMatches matches = new();
    private readonly FakeOperationLogger logger = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly CompanyAdminService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyAdminServiceTests"/> class.
    /// </summary>
    public CompanyAdminServiceTests()
    {
        this.service = new CompanyAdminService(
            this.companies,
            this.jobs,
            this.matches,
            this.logger,
            this.unitOfWork,
            new FakeClock { UtcNow = Now });
    }

    /// <summary>
    /// A suspensão passa a Empresa aprovada a suspensa, encerra as conversas abertas das suas vagas com o motivo
    /// <c>COMPANY_SUSPENDED</c> e sem autor, mantém os matches e as conversas já encerradas, e regista
    /// <c>COMPANY_SUSPENDED</c> com o Administrador, numa única gravação (RF095, RF075, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SuspendSuspendsTheCompanyClosesTheOpenConversationsAndLogsTheOperation()
    {
        Company company = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Approved);
        Match open = OpenConversation();
        Match closedByParty = OpenConversation();
        closedByParty.CloseConversation(Guid.NewGuid(), ConversationCloseReason.ClosedByParty, Created);
        this.matches.ByCompany[company.Id] = [open, closedByParty];

        await this.service.SuspendAsync(AdminId, company.Id, TestContext.Current.CancellationToken);

        Assert.Equal(CompanyStatus.Suspended, company.Status);
        Assert.Equal(MatchStatus.Matched, open.Status);
        Assert.Equal(ConversationStatus.Closed, open.ConversationStatus);
        Assert.Equal(ConversationCloseReason.CompanySuspended, open.ConversationCloseReason);
        Assert.Null(open.ConversationClosedById);
        Assert.Equal(Now, open.ConversationClosedAt);
        Assert.Equal(ConversationCloseReason.ClosedByParty, closedByParty.ConversationCloseReason);
        Assert.Equal(Created, closedByParty.ConversationClosedAt);
        Assert.Equal([(AdminId, OperationType.CompanySuspended, company.Id)], this.logger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Só uma Empresa aprovada pode ser suspensa: <c>409</c> (ERR-30) com o estado atual e nada é gravado.
    /// </summary>
    /// <param name="status">Estado atual da Empresa.</param>
    /// <param name="label">Designação do estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending, "pendente")]
    [InlineData(CompanyStatus.Rejected, "recusada")]
    [InlineData(CompanyStatus.Suspended, "suspensa")]
    public async Task SuspendingACompanyThatIsNotApprovedIsAnInvalidState(CompanyStatus status, string label)
    {
        Company company = this.AddCompany("Lumen Software, Lda.", status);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.SuspendAsync(AdminId, company.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-30", exception.Error.Code);
        Assert.Equal($"A operação não é permitida no estado atual: {label}.", exception.Error.Message);
        Assert.Equal(status, company.Status);
        this.AssertNothingSaved();
    }

    /// <summary>
    /// A reativação passa a Empresa suspensa a aprovada e regista <c>COMPANY_REACTIVATED</c>; as conversas encerradas
    /// mantêm-se em consulta, porque não são lidas nem alteradas (RF116, RF075, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReactivateApprovesTheSuspendedCompanyAndKeepsTheConversationsClosed()
    {
        Company company = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Suspended);

        await this.service.ReactivateAsync(AdminId, company.Id, TestContext.Current.CancellationToken);

        Assert.Equal(CompanyStatus.Approved, company.Status);
        Assert.Equal(0, this.matches.Queries);
        Assert.Equal([(AdminId, OperationType.CompanyReactivated, company.Id)], this.logger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Só uma Empresa suspensa pode ser reativada: <c>409</c> (ERR-30) com o estado atual e nada é gravado.
    /// </summary>
    /// <param name="status">Estado atual da Empresa.</param>
    /// <param name="label">Designação do estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending, "pendente")]
    [InlineData(CompanyStatus.Approved, "aprovada")]
    [InlineData(CompanyStatus.Rejected, "recusada")]
    public async Task ReactivatingACompanyThatIsNotSuspendedIsAnInvalidState(CompanyStatus status, string label)
    {
        Company company = this.AddCompany("Lumen Software, Lda.", status);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.ReactivateAsync(AdminId, company.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-30", exception.Error.Code);
        Assert.Equal($"A operação não é permitida no estado atual: {label}.", exception.Error.Message);
        this.AssertNothingSaved();
    }

    /// <summary>
    /// Uma Empresa inexistente dá <c>404</c> (ERR-20) na suspensão e na reativação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownCompanyIsNotFound()
    {
        Guid unknown = Guid.NewGuid();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.SuspendAsync(AdminId, unknown, cancellationToken));
        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.ReactivateAsync(AdminId, unknown, cancellationToken));
        this.AssertNothingSaved();
    }

    /// <summary>
    /// Se uma conversa da Empresa for alterada por outro pedido entre a leitura e a gravação, a suspensão é recusada com
    /// <c>409</c> (ERR-41) e nada fica gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrentChangeToAConversationIsAConflict()
    {
        Company company = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Approved);
        this.matches.ByCompany[company.Id] = [OpenConversation()];
        this.unitOfWork.FailWith = new ConcurrencyConflictException(new InvalidOperationException("xmin"));

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.SuspendAsync(AdminId, company.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-41", exception.Error.Code);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// A lista global tem todas as Empresas, em qualquer estado, com a designação, o estado e o número de vagas
    /// publicadas, por ordem alfabética portuguesa da designação (RF097).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListCompaniesReturnsEveryCompanySortedByName()
    {
        Company zeta = this.AddCompany("Zeta Logística, S.A.", CompanyStatus.Suspended);
        Company lumen = this.AddCompany("lumen Software, Lda.", CompanyStatus.Approved);
        Company agua = this.AddCompany("Águas do Minho, Lda.", CompanyStatus.Pending);
        this.companies.PublishedJobs[lumen.Id] = 4;
        this.companies.PublishedJobs[zeta.Id] = 1;

        List<CompanyAdminDto> list = await this.service.ListCompaniesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new CompanyAdminDto(agua.Id, "Águas do Minho, Lda.", CompanyStatus.Pending, 0),
                new CompanyAdminDto(lumen.Id, "lumen Software, Lda.", CompanyStatus.Approved, 4),
                new CompanyAdminDto(zeta.Id, "Zeta Logística, S.A.", CompanyStatus.Suspended, 1),
            ],
            list);
    }

    /// <summary>
    /// A lista global de vagas publicadas tem a função, a Empresa, a data de publicação e o número de interesses, da
    /// publicação mais recente para a mais antiga (RF098).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListPublishedJobsReturnsTheFourFieldsFromTheMostRecentPublication()
    {
        Company lumen = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Approved);
        Company zeta = this.AddCompany("Zeta Logística, S.A.", CompanyStatus.Approved);
        Job older = NewPublishedJob("Programador Backend .NET", Now.AddDays(-20), lumen.Id);
        Job newer = NewPublishedJob("Técnico de Logística", Now.AddDays(-2), zeta.Id);
        this.jobs.Published.AddRange(
            [
                new JobWithCount(older, 3),
                new JobWithCount(newer, 0),
            ]);

        List<PublishedJobAdminDto> list = await this.service.ListPublishedJobsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new PublishedJobAdminDto(newer.Id, "Técnico de Logística", "Zeta Logística, S.A.", Now.AddDays(-2), 0),
                new PublishedJobAdminDto(older.Id, "Programador Backend .NET", "Lumen Software, Lda.", Now.AddDays(-20), 3),
            ],
            list);
    }

    private static Match OpenConversation()
    {
        Match match = Match.ExpressInterest(Guid.NewGuid(), Guid.NewGuid(), Created);
        match.OpenProfile(Created);
        match.Accept(Guid.NewGuid(), Created);
        return match;
    }

    private static Job NewPublishedJob(string title, DateTimeOffset publishedAt, Guid companyId)
    {
        var job = new Job(
            companyId,
            new JobData
            {
                Title = title,
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = Guid.NewGuid(),
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Hybrid,
            });
        job.Publish(publishedAt);
        return job;
    }

    private Company AddCompany(string name, CompanyStatus status)
    {
        var company = new Company(
            Guid.NewGuid(),
            new CompanyRegistration
            {
                CompanyName = name,
                TaxId = Interlocked.Increment(ref taxIdCounter).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Industry = Industry.Technology,
                Address = "Avenida da Liberdade, 100, 4710-000 Braga",
                LocationId = Guid.NewGuid(),
                ContactEmail = "geral@exemplo.pt",
                ContactPhone = "253000111",
                ResponsibleName = "Rui Matos",
            },
            Created);
        switch (status)
        {
            case CompanyStatus.Approved:
                company.Approve();
                break;
            case CompanyStatus.Rejected:
                company.Reject("Documentação em falta no pedido.");
                break;
            case CompanyStatus.Suspended:
                company.Approve();
                company.Suspend();
                break;
            default:
                break;
        }

        this.companies.All.Add(company);
        return company;
    }

    private void AssertNothingSaved()
    {
        Assert.Empty(this.logger.Entries);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    private sealed class FakeCompanies : ICompanyRepository
    {
        public List<Company> All { get; } = [];

        public Dictionary<Guid, int> PublishedJobs { get; } = [];

        public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.All.SingleOrDefault(company => company.Id == id));
        }

        public Task<List<CompanyWithCount>> ListAllWithPublishedCountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.All
                .Select(company => new CompanyWithCount(company, this.PublishedJobs.GetValueOrDefault(company.Id)))
                .ToList());
        }
    }

    private sealed class FakeJobs : IJobRepository
    {
        public List<JobWithCount> Published { get; } = [];

        public Task<JobWithCompanyStatus?> GetWithCompanyStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<JobWithCount>> ListPublishedWithInterestCountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Published.ToList());
        }
    }

    private sealed class FakeMatches : IMatchRepository
    {
        public Dictionary<Guid, List<Match>> ByCompany { get; } = [];

        public int Queries { get; private set; }

        public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Match match, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Match>> ListMatchesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            this.Queries++;
            return Task.FromResult(this.ByCompany.GetValueOrDefault(companyId) ?? []);
        }

        public Task<List<Match>> ListOpenConversationsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeOperationLogger : IOperationLogger
    {
        public List<(Guid? UserId, OperationType Operation, Guid EntityId)> Entries { get; } = [];

        public Task LogAsync(
            Guid? userId,
            OperationType operation,
            Guid entityId,
            CancellationToken cancellationToken = default)
        {
            this.Entries.Add((userId, operation, entityId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public Exception? FailWith { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (this.FailWith is not null)
            {
                throw this.FailWith;
            }

            this.Saves++;
            return Task.FromResult(1);
        }

        public void DiscardChanges()
        {
            throw new NotSupportedException();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
