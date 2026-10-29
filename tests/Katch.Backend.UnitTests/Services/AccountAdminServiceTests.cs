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
/// Testes da administração das contas em <see cref="AccountAdminService"/> (documentação da API v02, secção 6.10;
/// RF075, RF085 a RF090, RF112), com repositórios em memória.
/// </summary>
public sealed class AccountAdminServiceTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 9, 14, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.Parse("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c01");

    private readonly FakeUsers users = new();
    private readonly FakeCandidates candidates = new();
    private readonly FakeReferenceData referenceData = new();
    private readonly FakeMatches matches = new();
    private readonly FakeOperationLogger logger = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly AccountAdminService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountAdminServiceTests"/> class.
    /// </summary>
    public AccountAdminServiceTests()
    {
        this.service = new AccountAdminService(
            this.users,
            this.candidates,
            this.referenceData,
            this.matches,
            this.logger,
            this.unitOfWork,
            new FakeClock { UtcNow = Now });
    }

    /// <summary>
    /// Gets the account types other than Candidate, which the suspension route treats as not found (6.10.4).
    /// </summary>
    public static TheoryData<UserType> NotCandidates => new() { UserType.Recruiter, UserType.Admin };

    /// <summary>
    /// A lista de contas tem o correio, o tipo e o estado de todas as contas, por ordem alfabética do correio, sem
    /// distinção de maiúsculas (RF085).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListAccountsReturnsEveryAccountSortedByEmail()
    {
        AppUser recruiter = this.AddUser(UserType.Recruiter, "recrutamento@lumen-exemplo.pt");
        AppUser candidate = this.AddUser(UserType.Candidate, "Ana.Ferreira@exemplo.pt");
        AppUser admin = this.AddUser(UserType.Admin, "admin@katch.pt");
        candidate.Block(Now);

        List<AccountDto> accounts = await this.service.ListAccountsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new AccountDto(admin.Id, "admin@katch.pt", UserType.Admin, AccountStatus.Active),
                new AccountDto(candidate.Id, "Ana.Ferreira@exemplo.pt", UserType.Candidate, AccountStatus.Blocked),
                new AccountDto(recruiter.Id, "recrutamento@lumen-exemplo.pt", UserType.Recruiter, AccountStatus.Active),
            ],
            accounts);
    }

    /// <summary>
    /// A lista de Candidatos tem o nome, o correio, a localidade com o distrito, a data de registo da conta e o estado, por
    /// ordem alfabética portuguesa do nome (RF088).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListCandidatesReturnsTheFiveFieldsSortedByName()
    {
        var guimaraes = new Location("Guimarães", "Braga", 41.4425m, -8.2918m);
        var lisboa = new Location("Lisboa", "Lisboa", 38.7223m, -9.1393m);
        this.referenceData.Locations.AddRange([guimaraes, lisboa]);
        Candidate rui = this.AddCandidate("Rui Matos", "rui@exemplo.pt", lisboa.Id);
        Candidate ana = this.AddCandidate("Ana Ferreira", "ana@exemplo.pt", guimaraes.Id);
        Candidate alvaro = this.AddCandidate("Álvaro Costa", "alvaro@exemplo.pt", lisboa.Id);
        rui.User.Suspend(Now);

        List<CandidateAdminDto> list = await this.service.ListCandidatesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new CandidateAdminDto(alvaro.Id, "Álvaro Costa", "alvaro@exemplo.pt", "Lisboa (Lisboa)", Created, AccountStatus.Active),
                new CandidateAdminDto(ana.Id, "Ana Ferreira", "ana@exemplo.pt", "Guimarães (Braga)", Created, AccountStatus.Active),
                new CandidateAdminDto(rui.Id, "Rui Matos", "rui@exemplo.pt", "Lisboa (Lisboa)", Created, AccountStatus.Suspended),
            ],
            list);
    }

    /// <summary>
    /// O bloqueio de um Candidato ativo passa a conta a bloqueada, encerra as conversas abertas da conta com o motivo
    /// <c>ACCOUNT_BLOCKED_OR_SUSPENDED</c> e sem autor, e regista <c>ACCOUNT_BLOCKED</c> com o Administrador, numa
    /// única gravação (RF086, RF075, RF112).
    /// </summary>
    /// <param name="userType">Tipo da conta bloqueada.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Recruiter)]
    public async Task BlockBlocksTheAccountClosesItsOpenConversationsAndLogsTheOperation(UserType userType)
    {
        AppUser user = this.AddUser(userType, "conta@exemplo.pt");
        Match open = OpenConversation();
        Match alreadyClosed = OpenConversation();
        alreadyClosed.CloseConversation(Guid.NewGuid(), ConversationCloseReason.ClosedByParty, Created);
        this.matches.OpenByUser[user.Id] = [open];

        await this.service.BlockAsync(AdminId, user.Id, TestContext.Current.CancellationToken);

        Assert.Equal(AccountStatus.Blocked, user.Status);
        Assert.Equal(Now, user.UpdatedAt);
        Assert.Equal(ConversationStatus.Closed, open.ConversationStatus);
        Assert.Equal(ConversationCloseReason.AccountBlockedOrSuspended, open.ConversationCloseReason);
        Assert.Null(open.ConversationClosedById);
        Assert.Equal(Now, open.ConversationClosedAt);
        Assert.Equal(ConversationCloseReason.ClosedByParty, alreadyClosed.ConversationCloseReason);
        Assert.Equal([(AdminId, OperationType.AccountBlocked, user.Id)], this.logger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// A conta de Administrador não pode ser bloqueada: <c>409</c> (ERR-30) e nada é alterado nem gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BlockingTheAdministratorIsAnInvalidState()
    {
        AppUser admin = this.AddUser(UserType.Admin, "admin@katch.pt");

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.BlockAsync(AdminId, admin.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-30", exception.Error.Code);
        Assert.Equal("A operação não é permitida no estado atual: conta de Administrador.", exception.Error.Message);
        Assert.Equal(AccountStatus.Active, admin.Status);
        this.AssertNothingSaved();
    }

    /// <summary>
    /// Só uma conta ativa pode ser bloqueada ou suspensa: <c>409</c> (ERR-30) com o estado atual e nada é gravado.
    /// </summary>
    /// <param name="current">Estado atual da conta.</param>
    /// <param name="label">Designação do estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(AccountStatus.Blocked, "bloqueada")]
    [InlineData(AccountStatus.Suspended, "suspensa")]
    public async Task BlockingOrSuspendingAnAccountThatIsNotActiveIsAnInvalidState(AccountStatus current, string label)
    {
        AppUser user = this.AddUser(UserType.Candidate, "ana@exemplo.pt");
        ChangeStatus(user, current);
        string message = $"A operação não é permitida no estado atual: {label}.";

        OperationConflictException blocked = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.BlockAsync(AdminId, user.Id, TestContext.Current.CancellationToken));
        OperationConflictException suspended = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.SuspendCandidateAsync(AdminId, user.Id, TestContext.Current.CancellationToken));

        Assert.Equal(("ERR-30", message), (blocked.Error.Code, blocked.Error.Message));
        Assert.Equal(("ERR-30", message), (suspended.Error.Code, suspended.Error.Message));
        Assert.Equal(current, user.Status);
        this.AssertNothingSaved();
    }

    /// <summary>
    /// Uma conta inexistente dá <c>404</c> (ERR-20) no bloqueio, na suspensão e na reativação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownAccountIsNotFound()
    {
        Guid unknown = Guid.NewGuid();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.BlockAsync(AdminId, unknown, cancellationToken));
        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.SuspendCandidateAsync(AdminId, unknown, cancellationToken));
        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.ReactivateAsync(AdminId, unknown, cancellationToken));
        this.AssertNothingSaved();
    }

    /// <summary>
    /// A suspensão passa a conta ativa de um Candidato a suspensa, encerra as conversas abertas como o bloqueio e regista
    /// <c>ACCOUNT_SUSPENDED</c> (RF089, RF075, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SuspendCandidateSuspendsTheAccountClosesItsOpenConversationsAndLogsTheOperation()
    {
        AppUser user = this.AddUser(UserType.Candidate, "ana@exemplo.pt");
        Match open = OpenConversation();
        this.matches.OpenByUser[user.Id] = [open];

        await this.service.SuspendCandidateAsync(AdminId, user.Id, TestContext.Current.CancellationToken);

        Assert.Equal(AccountStatus.Suspended, user.Status);
        Assert.Equal(ConversationStatus.Closed, open.ConversationStatus);
        Assert.Equal(ConversationCloseReason.AccountBlockedOrSuspended, open.ConversationCloseReason);
        Assert.Equal([(AdminId, OperationType.AccountSuspended, user.Id)], this.logger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Só a conta de Candidato é suspensa: as restantes dão <c>404</c> (ERR-20), como uma conta inexistente (6.10.4).
    /// </summary>
    /// <param name="userType">Tipo da conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(NotCandidates))]
    public async Task SuspendingAnAccountThatIsNotACandidateIsNotFound(UserType userType)
    {
        AppUser user = this.AddUser(userType, "conta@exemplo.pt");

        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.SuspendCandidateAsync(AdminId, user.Id, TestContext.Current.CancellationToken));

        Assert.Equal(AccountStatus.Active, user.Status);
        this.AssertNothingSaved();
    }

    /// <summary>
    /// A reativação passa a conta bloqueada ou suspensa a ativa e regista <c>ACCOUNT_REACTIVATED</c>; as conversas
    /// encerradas mantêm-se em consulta (RF087, RF090, RF075, RF112).
    /// </summary>
    /// <param name="current">Estado atual da conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(AccountStatus.Blocked)]
    [InlineData(AccountStatus.Suspended)]
    public async Task ReactivateRestoresTheAccountAndKeepsTheConversationsClosed(AccountStatus current)
    {
        AppUser user = this.AddUser(UserType.Candidate, "ana@exemplo.pt");
        ChangeStatus(user, current);
        Match closed = OpenConversation();
        closed.CloseConversation(null, ConversationCloseReason.AccountBlockedOrSuspended, Created);
        this.matches.OpenByUser[user.Id] = [closed];

        await this.service.ReactivateAsync(AdminId, user.Id, TestContext.Current.CancellationToken);

        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.Equal(ConversationStatus.Closed, closed.ConversationStatus);
        Assert.False(closed.CanSendMessages());
        Assert.Equal(0, this.matches.Queries);
        Assert.Equal([(AdminId, OperationType.AccountReactivated, user.Id)], this.logger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Uma conta já ativa não é reativada: <c>409</c> (ERR-30) com o estado «ativa», também na conta de Administrador.
    /// </summary>
    /// <param name="userType">Tipo da conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Admin)]
    public async Task ReactivatingAnActiveAccountIsAnInvalidState(UserType userType)
    {
        AppUser user = this.AddUser(userType, "conta@exemplo.pt");

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.ReactivateAsync(AdminId, user.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-30", exception.Error.Code);
        Assert.Equal("A operação não é permitida no estado atual: ativa.", exception.Error.Message);
        this.AssertNothingSaved();
    }

    /// <summary>
    /// Se uma conversa da conta for alterada por outro pedido entre a leitura e a gravação, a operação é recusada com
    /// <c>409</c> (ERR-41) e nada fica gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrentChangeToAConversationIsAConflict()
    {
        AppUser user = this.AddUser(UserType.Candidate, "ana@exemplo.pt");
        this.matches.OpenByUser[user.Id] = [OpenConversation()];
        this.unitOfWork.FailWith = new ConcurrencyConflictException(new InvalidOperationException("xmin"));

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.BlockAsync(AdminId, user.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-41", exception.Error.Code);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// A designação dos estados da conta na mensagem do ERR-30.
    /// </summary>
    /// <param name="status">Estado da conta.</param>
    /// <param name="label">Designação esperada.</param>
    [Theory]
    [InlineData(AccountStatus.Active, "ativa")]
    [InlineData(AccountStatus.Blocked, "bloqueada")]
    [InlineData(AccountStatus.Suspended, "suspensa")]
    public void AccountStatusLabelNamesEachState(AccountStatus status, string label)
    {
        Assert.Equal(label, AccountAdminService.AccountStatusLabel(status));
    }

    private static Match OpenConversation()
    {
        Match match = Match.ExpressInterest(Guid.NewGuid(), Guid.NewGuid(), Created);
        match.OpenProfile(Created);
        match.Accept(Guid.NewGuid(), Created);
        return match;
    }

    private static void ChangeStatus(AppUser user, AccountStatus status)
    {
        if (status == AccountStatus.Blocked)
        {
            user.Block(Created);
        }
        else if (status == AccountStatus.Suspended)
        {
            user.Suspend(Created);
        }
    }

    private AppUser AddUser(UserType userType, string email)
    {
        var user = new AppUser(email, "hash", userType, userType == UserType.Admin ? null : Created, Created);
        this.users.Accounts.Add(user);
        return user;
    }

    private Candidate AddCandidate(string fullName, string email, Guid locationId)
    {
        AppUser user = this.AddUser(UserType.Candidate, email);
        var candidate = new Candidate(user, fullName, "912345678", locationId);
        this.candidates.All.Add(candidate);
        return candidate;
    }

    private void AssertNothingSaved()
    {
        Assert.Empty(this.logger.Entries);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    private sealed class FakeUsers : IUserRepository
    {
        public List<AppUser> Accounts { get; } = [];

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Accounts.SingleOrDefault(user => user.Id == id));
        }

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddRecruiterAsync(Recruiter recruiter, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<AppUser>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Accounts.ToList());
        }
    }

    private sealed class FakeCandidates : ICandidateRepository
    {
        public List<Candidate> All { get; } = [];

        public Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Candidate>> ListQuotaDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Candidate>> ListForAdminAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.All.ToList());
        }
    }

    private sealed class FakeReferenceData : IReferenceDataRepository
    {
        public List<Location> Locations { get; } = [];

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Locations.ToList());
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeMatches : IMatchRepository
    {
        public Dictionary<Guid, List<Match>> OpenByUser { get; } = [];

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
            throw new NotSupportedException();
        }

        public Task<List<Match>> ListOpenConversationsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            this.Queries++;
            return Task.FromResult(this.OpenByUser.GetValueOrDefault(userId) ?? []);
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
