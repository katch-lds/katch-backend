// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes do início de sessão e da alteração da palavra-passe em <see cref="AuthService"/> (documentação da API v02,
/// secções 6.1.3 e 6.1.5; RF001, RF002, RF038, RF040, RF083, RF084, RNF003, RNF009, P05), com repositórios em memória.
/// </summary>
public sealed class AuthServiceLoginTests
{
    private const string ValidPassword = "katch2026";
    private const string NewPassword = "nova2026";
    private const string Email = "ana.ferreira@exemplo.pt";

    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 30, 0, TimeSpan.Zero);

    private readonly FakeUsers users = new();
    private readonly SpyHasher hasher = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeTokens tokens = new();
    private readonly AuthService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthServiceLoginTests"/> class.
    /// </summary>
    public AuthServiceLoginTests()
    {
        this.service = new AuthService(
            this.users,
            new FakeCandidates(),
            new FakeReferenceData(),
            this.unitOfWork,
            this.hasher,
            this.tokens,
            new FixedClock(Now));
    }

    /// <summary>
    /// Cada tipo de conta entra pelo seu ponto de acesso e recebe a credencial emitida para essa conta.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="app">Ponto de acesso adequado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, ClientApp.Mobile)]
    [InlineData(UserType.Recruiter, ClientApp.Web)]
    [InlineData(UserType.Admin, ClientApp.Web)]
    public async Task EveryAccountTypeSignsInFromItsAccessPoint(UserType userType, ClientApp app)
    {
        AppUser user = this.AddUser(userType);

        LoginResponse response = await this.service.LoginAsync(Credentials(), app, TestContext.Current.CancellationToken);

        Assert.Same(user, this.tokens.IssuedFor);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(userType, response.UserType);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// O tipo de conta que entra pelo ponto de acesso errado é recusado depois de verificadas as credenciais, e não
    /// recebe credencial.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="app">Ponto de acesso errado para o tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, ClientApp.Web)]
    [InlineData(UserType.Recruiter, ClientApp.Mobile)]
    [InlineData(UserType.Admin, ClientApp.Mobile)]
    public async Task WrongAccessPointIsRejectedAfterTheCredentials(UserType userType, ClientApp app)
    {
        this.AddUser(userType);

        await this.AssertRejectedAsync(Credentials(), app, LoginRejection.WrongAccessPoint);

        Assert.Single(this.hasher.Verifications);
        Assert.Null(this.tokens.IssuedFor);
    }

    /// <summary>
    /// Um ponto de acesso desconhecido nunca dá acesso (falha fechada).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownAccessPointIsRejected()
    {
        this.AddUser(UserType.Candidate);

        await this.AssertRejectedAsync(Credentials(), (ClientApp)99, LoginRejection.WrongAccessPoint);
    }

    /// <summary>
    /// Com o correio inexistente a palavra-passe é verificada na mesma, contra o resumo de referência, para que o tempo
    /// de resposta não revele se o correio está registado (RNF009); a recusa é a das credenciais erradas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownEmailStillVerifiesAgainstTheReferenceHash()
    {
        this.AddUser(UserType.Candidate);

        await this.AssertRejectedAsync(
            new LoginRequest("outra.pessoa@exemplo.pt", ValidPassword),
            ClientApp.Mobile,
            LoginRejection.InvalidCredentials);

        (string hash, string password) = Assert.Single(this.hasher.Verifications);
        Assert.Equal(AuthService.ReferenceHash, hash);
        Assert.Equal(ValidPassword, password);
        Assert.Null(this.tokens.IssuedFor);
    }

    /// <summary>
    /// O resumo de referência tem os mesmos parâmetros dos resumos das contas (formato, algoritmo, iterações e tamanho do
    /// valor aleatório), senão a verificação sem conta seria mais rápida e voltava a distinguir os dois casos. Se uma
    /// atualização do framework mudar os parâmetros por omissão, este teste falha.
    /// </summary>
    [Fact]
    public void ReferenceHashHasTheSameParametersAsAccountHashes()
    {
        const int headerLength = 13;
        byte[] reference = Convert.FromBase64String(AuthService.ReferenceHash);
        byte[] account = Convert.FromBase64String(new IdentityPasswordHasher().Hash(ValidPassword));

        Assert.Equal(account.Length, reference.Length);
        Assert.Equal(account[..headerLength], reference[..headerLength]);
        Assert.False(new IdentityPasswordHasher().Verify(AuthService.ReferenceHash, ValidPassword));
    }

    /// <summary>
    /// Uma palavra-passe errada recebe a mesma recusa que um correio inexistente (RNF009).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WrongPasswordIsRejectedAsInvalidCredentials()
    {
        AppUser user = this.AddUser(UserType.Candidate);

        await this.AssertRejectedAsync(
            new LoginRequest(Email, "errada2026"),
            ClientApp.Mobile,
            LoginRejection.InvalidCredentials);

        Assert.Equal(user.PasswordHash, Assert.Single(this.hasher.Verifications).Hash);
        Assert.Null(this.tokens.IssuedFor);
    }

    /// <summary>
    /// A palavra-passe não é aparada: espaços a mais tornam-na outra palavra-passe.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PasswordIsNotTrimmed()
    {
        this.AddUser(UserType.Candidate);

        await this.AssertRejectedAsync(
            new LoginRequest(Email, $" {ValidPassword} "),
            ClientApp.Mobile,
            LoginRejection.InvalidCredentials);
    }

    /// <summary>
    /// O correio é comparado sem distinção de maiúsculas e sem os espaços do início e do fim.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmailIsTrimmedAndCaseInsensitive()
    {
        AppUser user = this.AddUser(UserType.Candidate);

        LoginResponse response = await this.service.LoginAsync(
            new LoginRequest("  ANA.Ferreira@EXEMPLO.pt ", ValidPassword),
            ClientApp.Mobile,
            TestContext.Current.CancellationToken);

        Assert.Equal(user.Id, response.UserId);
    }

    /// <summary>
    /// O estado da conta só é revelado com a palavra-passe correta: bloqueada dá o ERR-03 e suspensa o ERR-04; com a
    /// palavra-passe errada ambas dão credenciais erradas.
    /// </summary>
    /// <param name="suspended"><see langword="true"/> para suspender a conta; <see langword="false"/> para a bloquear.</param>
    /// <param name="expected">Recusa esperada com a palavra-passe correta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(false, LoginRejection.AccountBlocked)]
    [InlineData(true, LoginRejection.AccountSuspended)]
    public async Task AccountStateIsOnlyRevealedWithTheCorrectPassword(bool suspended, LoginRejection expected)
    {
        this.AddUser(
            UserType.Candidate,
            account =>
            {
                if (suspended)
                {
                    account.Suspend(Now);
                }
                else
                {
                    account.Block(Now);
                }
            });

        await this.AssertRejectedAsync(Credentials(), ClientApp.Mobile, expected);
        await this.AssertRejectedAsync(
            new LoginRequest(Email, "errada2026"),
            ClientApp.Mobile,
            LoginRejection.InvalidCredentials);
        Assert.Null(this.tokens.IssuedFor);
    }

    /// <summary>
    /// O estado da conta vem antes do ponto de acesso: uma conta bloqueada que entra pelo ponto errado recebe o ERR-03.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AccountStateIsCheckedBeforeTheAccessPoint()
    {
        this.AddUser(UserType.Candidate, account => account.Block(Now));

        await this.AssertRejectedAsync(Credentials(), ClientApp.Web, LoginRejection.AccountBlocked);
    }

    /// <summary>
    /// As credenciais vêm antes de tudo: uma conta bloqueada, no ponto errado, com a palavra-passe errada, recebe o ERR-02.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CredentialsAreCheckedBeforeStateAndAccessPoint()
    {
        this.AddUser(UserType.Candidate, account => account.Block(Now));

        await this.AssertRejectedAsync(
            new LoginRequest(Email, "errada2026"),
            ClientApp.Web,
            LoginRejection.InvalidCredentials);
    }

    /// <summary>
    /// Um correio ou uma palavra-passe vazios são indicados no mesmo ERR-10, sem consultar a base de dados.
    /// </summary>
    /// <param name="email">Correio enviado.</param>
    /// <param name="password">Palavra-passe enviada.</param>
    /// <param name="expectedFields">Campos esperados em erro.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null, null, "email,password")]
    [InlineData("", "", "email,password")]
    [InlineData("   ", "      ", "email,password")]
    [InlineData(Email, null, "password")]
    [InlineData(null, ValidPassword, "email")]
    public async Task EmptyFieldsAreRejectedWithoutLookingUpTheAccount(string? email, string? password, string expectedFields)
    {
        this.AddUser(UserType.Candidate);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.LoginAsync(new LoginRequest(email, password), ClientApp.Mobile, TestContext.Current.CancellationToken));

        Assert.Equal(expectedFields.Split(','), exception.Fields.Select(field => field.Field));
        Assert.All(exception.Fields, field => Assert.Equal(FieldReasons.Required, field.Reason));
        Assert.Empty(this.hasher.Verifications);
        Assert.Equal(0, this.users.Lookups);
    }

    /// <summary>
    /// O formato do correio e a política da palavra-passe não são revelados: valores fora do formato são credenciais
    /// erradas (RNF009).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmailFormatAndPasswordPolicyAreNotRevealed()
    {
        await this.AssertRejectedAsync(
            new LoginRequest("isto-nao-e-um-correio", "x"),
            ClientApp.Mobile,
            LoginRejection.InvalidCredentials);
    }

    /// <summary>
    /// A recusa nunca leva o correio nem a palavra-passe na mensagem (RNF017).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectionNeverCarriesTheEmailOrThePassword()
    {
        this.AddUser(UserType.Candidate);

        LoginRejectedException exception = await Assert.ThrowsAsync<LoginRejectedException>(
            () => this.service.LoginAsync(
                new LoginRequest(Email, "errada2026"),
                ClientApp.Mobile,
                TestContext.Current.CancellationToken));

        Assert.DoesNotContain(Email, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("errada2026", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// Com a palavra-passe atual correta, o resumo da nova substitui o antigo, a data da alteração fica registada e a
    /// palavra-passe nova nunca é guardada (RNF003).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ChangePasswordReplacesTheHashAndRecordsTheDate()
    {
        AppUser user = this.AddUser(UserType.Recruiter);
        string previousHash = user.PasswordHash;

        await this.service.ChangePasswordAsync(
            user.Id,
            new ChangePasswordRequest(ValidPassword, NewPassword),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(previousHash, user.PasswordHash);
        Assert.DoesNotContain(NewPassword, user.PasswordHash, StringComparison.Ordinal);
        Assert.True(this.hasher.Verify(user.PasswordHash, NewPassword));
        Assert.False(this.hasher.Verify(user.PasswordHash, ValidPassword));
        Assert.Equal(Now, user.UpdatedAt);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// A palavra-passe atual errada é recusada no campo <c>currentPassword</c> e nada é gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WrongCurrentPasswordIsRejectedAndNothingIsSaved()
    {
        AppUser user = this.AddUser(UserType.Candidate);
        string previousHash = user.PasswordHash;

        IReadOnlyList<FieldErrorDto> fields = await this.ChangeExpectingErrorsAsync(
            user.Id,
            new ChangePasswordRequest("errada2026", NewPassword));

        Assert.Equal(new FieldErrorDto("currentPassword", FieldReasons.CurrentIncorrect), Assert.Single(fields));
        Assert.Equal(previousHash, user.PasswordHash);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Uma palavra-passe nova fora do P05 é recusada no campo <c>newPassword</c>, e nada é gravado.
    /// </summary>
    /// <param name="newPassword">Palavra-passe nova.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("katch", FieldReasons.PasswordPolicy)]
    [InlineData("katch20", FieldReasons.PasswordPolicy)]
    [InlineData("katchkatch", FieldReasons.PasswordPolicy)]
    [InlineData("2026202620", FieldReasons.PasswordPolicy)]
    [InlineData("", FieldReasons.Required)]
    [InlineData("        ", FieldReasons.Required)]
    public async Task NewPasswordOutsideP05IsRejected(string newPassword, string reason)
    {
        AppUser user = this.AddUser(UserType.Candidate);

        IReadOnlyList<FieldErrorDto> fields = await this.ChangeExpectingErrorsAsync(
            user.Id,
            new ChangePasswordRequest(ValidPassword, newPassword));

        Assert.Equal(new FieldErrorDto("newPassword", reason), Assert.Single(fields));
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Os limites do P05 são aceites: 8 e 128 caracteres; 129 é recusado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NewPasswordLengthBoundariesAreEnforced()
    {
        AppUser user = this.AddUser(UserType.Candidate);
        string longest = "k1" + new string('a', 126);

        await this.service.ChangePasswordAsync(
            user.Id,
            new ChangePasswordRequest(ValidPassword, "katch202"),
            TestContext.Current.CancellationToken);
        await this.service.ChangePasswordAsync(
            user.Id,
            new ChangePasswordRequest("katch202", longest),
            TestContext.Current.CancellationToken);
        IReadOnlyList<FieldErrorDto> fields = await this.ChangeExpectingErrorsAsync(
            user.Id,
            new ChangePasswordRequest(longest, longest + "a"));

        Assert.Equal(128, longest.Length);
        Assert.Equal(new FieldErrorDto("newPassword", FieldReasons.PasswordTooLong), Assert.Single(fields));
        Assert.Equal(2, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Com a atual errada e a nova inválida, os dois campos são indicados juntos, pela ordem do pedido.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BothFieldsAreReportedTogether()
    {
        AppUser user = this.AddUser(UserType.Candidate);

        IReadOnlyList<FieldErrorDto> fields = await this.ChangeExpectingErrorsAsync(
            user.Id,
            new ChangePasswordRequest("errada2026", "katch"));

        Assert.Equal(
            [
                new FieldErrorDto("currentPassword", FieldReasons.CurrentIncorrect),
                new FieldErrorDto("newPassword", FieldReasons.PasswordPolicy),
            ],
            fields);
    }

    /// <summary>
    /// Sem nenhum campo, os dois são indicados como obrigatórios, sem verificar a palavra-passe.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmptyRequestReportsBothFieldsAsRequired()
    {
        AppUser user = this.AddUser(UserType.Candidate);
        this.hasher.Verifications.Clear();

        IReadOnlyList<FieldErrorDto> fields = await this.ChangeExpectingErrorsAsync(user.Id, ChangePasswordRequest.Empty);

        Assert.Equal(
            [
                new FieldErrorDto("currentPassword", FieldReasons.Required),
                new FieldErrorDto("newPassword", FieldReasons.Required),
            ],
            fields);
        Assert.Empty(this.hasher.Verifications);
    }

    /// <summary>
    /// Uma credencial de uma conta que não existe é um erro inesperado, nunca uma alteração silenciosa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ChangePasswordOfNonexistentAccountFails()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => this.service.ChangePasswordAsync(
                Guid.NewGuid(),
                new ChangePasswordRequest(ValidPassword, NewPassword),
                TestContext.Current.CancellationToken));

        Assert.Equal(0, this.unitOfWork.Saves);
    }

    private static LoginRequest Credentials()
    {
        return new LoginRequest(Email, ValidPassword);
    }

    private AppUser AddUser(UserType userType, Action<AppUser>? configure = null)
    {
        var user = new AppUser(Email, this.hasher.Hash(ValidPassword), userType, Now, Now);
        configure?.Invoke(user);
        this.users.Accounts.Add(user);
        this.hasher.Verifications.Clear();
        return user;
    }

    private async Task AssertRejectedAsync(LoginRequest request, ClientApp app, LoginRejection expected)
    {
        LoginRejectedException exception = await Assert.ThrowsAsync<LoginRejectedException>(
            () => this.service.LoginAsync(request, app, TestContext.Current.CancellationToken));

        Assert.Equal(expected, exception.Reason);
    }

    private async Task<IReadOnlyList<FieldErrorDto>> ChangeExpectingErrorsAsync(Guid userId, ChangePasswordRequest request)
    {
        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.ChangePasswordAsync(userId, request, TestContext.Current.CancellationToken));
        return exception.Fields;
    }

    private sealed class SpyHasher : IPasswordHasher
    {
        private readonly IdentityPasswordHasher inner = new();

        public List<(string Hash, string Password)> Verifications { get; } = [];

        public string Hash(string password)
        {
            return this.inner.Hash(password);
        }

        public bool Verify(string hash, string password)
        {
            this.Verifications.Add((hash, password));
            return this.inner.Verify(hash, password);
        }
    }

    private sealed class FakeUsers : IUserRepository
    {
        public List<AppUser> Accounts { get; } = [];

        public int Lookups { get; private set; }

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            this.Lookups++;
            return Task.FromResult(this.Accounts.SingleOrDefault(user => user.Id == id));
        }

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            this.Lookups++;
            return Task.FromResult(
                this.Accounts.SingleOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        {
            this.Lookups++;
            return Task.FromResult(this.Accounts.Exists(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));
        }

        public Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
        {
            this.Accounts.Add(user);
            return Task.CompletedTask;
        }

        public Task AddRecruiterAsync(Recruiter recruiter, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountActiveCandidatesAtAsync(DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeCandidates : ICandidateRepository
    {
        public Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Candidate>> ListQuotaDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<Candidate>());
        }
    }

    private sealed class FakeReferenceData : IReferenceDataRepository
    {
        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<Location>());
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<List<Benefit>> ListBenefitsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Benefit?> GetBenefitAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> SkillNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> BenefitNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddSkillAsync(Skill skill, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddBenefitAsync(Benefit benefit, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            this.Saves++;
            return Task.FromResult(1);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void DiscardChanges()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeTokens : ITokenService
    {
        public AppUser? IssuedFor { get; private set; }

        public LoginResponse CreateToken(AppUser user)
        {
            this.IssuedFor = user;
            return new LoginResponse("token", Now.AddHours(8), user.UserType, user.Id);
        }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now)
        {
            this.UtcNow = now;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
