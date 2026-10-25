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
/// Testes do registo do Candidato em <see cref="AuthService"/> (documentação da API v02, secção 6.1.1; RF003 a RF005,
/// RNF003, P04, P05), com repositórios em memória.
/// </summary>
public sealed class AuthServiceTests
{
    private const string ValidPassword = "katch2026";

    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid Guimaraes = Guid.Parse("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c08");

    private readonly FakeUsers users = new();
    private readonly FakeCandidates candidates = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeTokens tokens = new();
    private readonly AuthService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthServiceTests"/> class.
    /// </summary>
    public AuthServiceTests()
    {
        this.service = new AuthService(
            this.users,
            this.candidates,
            new FakeReferenceData(Guimaraes),
            this.unitOfWork,
            new IdentityPasswordHasher(),
            this.tokens,
            new FixedClock(Now));
    }

    /// <summary>
    /// Com dados válidos, a conta fica ativa, com a quota de 10 interesses e a sessão iniciada.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ValidRequestCreatesActiveCandidateWithFullQuotaAndReturnsToken()
    {
        LoginResponse response = await this.service.RegisterCandidateAsync(Valid(), TestContext.Current.CancellationToken);

        AppUser user = Assert.Single(this.users.Added);
        Candidate candidate = Assert.Single(this.candidates.Added);
        Assert.Equal(1, this.unitOfWork.Saves);
        Assert.Equal(UserType.Candidate, user.UserType);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.Equal(Now, user.TermsAcceptedAt);
        Assert.Equal(Now, user.CreatedAt);
        Assert.Equal("ana.ferreira@exemplo.pt", user.Email);
        Assert.Equal(user.Id, candidate.Id);
        Assert.Equal("Ana Ferreira", candidate.FullName);
        Assert.Equal("912345678", candidate.PhoneNumber);
        Assert.Equal(Guimaraes, candidate.LocationId);
        Assert.Equal(Candidate.MaxInterests, candidate.SwipeRightsRemaining);
        Assert.Null(candidate.NextSwipeAvailableAt);
        Assert.Same(user, this.tokens.IssuedFor);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(UserType.Candidate, response.UserType);
    }

    /// <summary>
    /// A palavra-passe fica só como resumo, verificável e diferente da palavra-passe (RNF003).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PasswordIsStoredOnlyAsHash()
    {
        await this.service.RegisterCandidateAsync(Valid(), TestContext.Current.CancellationToken);

        AppUser user = Assert.Single(this.users.Added);
        Assert.NotEqual(ValidPassword, user.PasswordHash);
        Assert.DoesNotContain(ValidPassword, user.PasswordHash, StringComparison.Ordinal);
        Assert.True(new IdentityPasswordHasher().Verify(user.PasswordHash, ValidPassword));
    }

    /// <summary>
    /// Os espaços no início e no fim dos textos são retirados antes da validação e da gravação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TextsAreTrimmedBeforeValidationAndStorage()
    {
        RegisterCandidateRequest request = Valid() with
        {
            FullName = "  Ana Ferreira ",
            Email = " ana.ferreira@exemplo.pt ",
            PhoneNumber = " 912345678 ",
        };

        await this.service.RegisterCandidateAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("ana.ferreira@exemplo.pt", Assert.Single(this.users.Added).Email);
        Candidate candidate = Assert.Single(this.candidates.Added);
        Assert.Equal("Ana Ferreira", candidate.FullName);
        Assert.Equal("912345678", candidate.PhoneNumber);
    }

    /// <summary>
    /// Cada campo inválido é recusado com o motivo do catálogo, e nada é gravado.
    /// </summary>
    /// <param name="field">Campo em erro.</param>
    /// <param name="value">Valor enviado.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("fullName", "", FieldReasons.Required)]
    [InlineData("fullName", "   ", FieldReasons.Required)]
    [InlineData("email", "", FieldReasons.Required)]
    [InlineData("email", "ana.ferreira", FieldReasons.EmailFormat)]
    [InlineData("email", "ana@@exemplo.pt", FieldReasons.EmailFormat)]
    [InlineData("email", "ana ferreira@exemplo.pt", FieldReasons.EmailFormat)]
    [InlineData("email", "@exemplo.pt", FieldReasons.EmailFormat)]
    [InlineData("email", "ana@", FieldReasons.EmailFormat)]
    [InlineData("phoneNumber", "", FieldReasons.Required)]
    [InlineData("phoneNumber", "91234567", FieldReasons.PhoneNumberFormat)]
    [InlineData("phoneNumber", "9123456789", FieldReasons.PhoneNumberFormat)]
    [InlineData("phoneNumber", "91234567a", FieldReasons.PhoneNumberFormat)]
    [InlineData("phoneNumber", "912 345 678", FieldReasons.PhoneNumberFormat)]
    [InlineData("password", "", FieldReasons.Required)]
    [InlineData("password", "        ", FieldReasons.Required)]
    [InlineData("password", "katch20", FieldReasons.PasswordPolicy)]
    [InlineData("password", "katchkatch", FieldReasons.PasswordPolicy)]
    [InlineData("password", "2026202620", FieldReasons.PasswordPolicy)]
    public async Task InvalidFieldIsRejectedWithItsReason(string field, string value, string reason)
    {
        RegisterCandidateRequest request = field switch
        {
            "fullName" => Valid() with { FullName = value },
            "email" => Valid() with { Email = value },
            "phoneNumber" => Valid() with { PhoneNumber = value },
            _ => Valid() with { Password = value },
        };

        IReadOnlyList<FieldErrorDto> fields = await this.RegisterExpectingErrorsAsync(request);

        Assert.Equal(new FieldErrorDto(field, reason), Assert.Single(fields));
        Assert.Empty(this.users.Added);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Os comprimentos máximos do nome (150), do correio (255) e da palavra-passe (128) são verificados.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TooLongValuesAreRejected()
    {
        RegisterCandidateRequest request = Valid() with
        {
            FullName = new string('a', 151),
            Email = new string('a', 245) + "@exemplo.pt",
            Password = new string('a', 128) + "1",
        };

        IReadOnlyList<FieldErrorDto> fields = await this.RegisterExpectingErrorsAsync(request);

        Assert.Equal(
            [
                new FieldErrorDto("fullName", FieldReasons.FullNameTooLong),
                new FieldErrorDto("email", FieldReasons.EmailTooLong),
                new FieldErrorDto("password", FieldReasons.PasswordTooLong),
            ],
            fields);
    }

    /// <summary>
    /// Os valores no limite são aceites: nome com 150 caracteres e palavra-passe com 8 e com 128.
    /// </summary>
    /// <param name="password">Palavra-passe no limite.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("katch202")]
    [InlineData("k2345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678")]
    public async Task BoundaryValuesAreAccepted(string password)
    {
        RegisterCandidateRequest request = Valid() with { FullName = new string('a', 150), Password = password };

        await this.service.RegisterCandidateAsync(request, TestContext.Current.CancellationToken);

        Assert.Single(this.users.Added);
    }

    /// <summary>
    /// O correio já registado é recusado, mesmo com maiúsculas diferentes (RF004, RF037).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmailAlreadyRegisteredWithOtherCaseIsRejected()
    {
        this.users.Existing.Add("ana.ferreira@exemplo.pt");

        IReadOnlyList<FieldErrorDto> fields = await this.RegisterExpectingErrorsAsync(
            Valid() with { Email = "Ana.Ferreira@EXEMPLO.pt" });

        Assert.Equal(new FieldErrorDto("email", FieldReasons.EmailTaken), Assert.Single(fields));
    }

    /// <summary>
    /// Uma localidade em falta ou fora da lista pré-definida é recusada.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LocationMissingOrOutsideTheListIsRejected()
    {
        IReadOnlyList<FieldErrorDto> missing = await this.RegisterExpectingErrorsAsync(Valid() with { LocationId = Guid.Empty });
        IReadOnlyList<FieldErrorDto> unknown = await this.RegisterExpectingErrorsAsync(Valid() with { LocationId = Guid.NewGuid() });

        Assert.Equal(new FieldErrorDto("locationId", FieldReasons.Required), Assert.Single(missing));
        Assert.Equal(new FieldErrorDto("locationId", FieldReasons.NotInList), Assert.Single(unknown));
    }

    /// <summary>
    /// Sem a aceitação das condições de utilização, o registo é recusado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TermsNotAcceptedAreRejected()
    {
        IReadOnlyList<FieldErrorDto> fields = await this.RegisterExpectingErrorsAsync(Valid() with { AcceptTerms = false });

        Assert.Equal(new FieldErrorDto("acceptTerms", FieldReasons.AcceptTerms), Assert.Single(fields));
    }

    /// <summary>
    /// Todos os campos em erro são indicados na mesma resposta, pela ordem do formulário (RF004).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AllInvalidFieldsAreReportedTogether()
    {
        IReadOnlyList<FieldErrorDto> fields = await this.RegisterExpectingErrorsAsync(RegisterCandidateRequest.Empty);

        Assert.Equal(
            [
                new FieldErrorDto("fullName", FieldReasons.Required),
                new FieldErrorDto("email", FieldReasons.Required),
                new FieldErrorDto("phoneNumber", FieldReasons.Required),
                new FieldErrorDto("locationId", FieldReasons.Required),
                new FieldErrorDto("password", FieldReasons.Required),
                new FieldErrorDto("acceptTerms", FieldReasons.AcceptTerms),
            ],
            fields);
    }

    /// <summary>
    /// Se outro pedido gravar o mesmo correio entre a verificação e a gravação, o índice único transforma-se no mesmo
    /// erro de campo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrentRegistrationOfTheSameEmailIsReportedAsEmailTaken()
    {
        this.unitOfWork.FailWith = new UniqueConstraintViolationException(AuthService.EmailUniqueIndex, new InvalidOperationException());

        IReadOnlyList<FieldErrorDto> fields = await this.RegisterExpectingErrorsAsync(Valid());

        Assert.Equal(new FieldErrorDto("email", FieldReasons.EmailTaken), Assert.Single(fields));
        Assert.Null(this.tokens.IssuedFor);
    }

    private static RegisterCandidateRequest Valid()
    {
        return new RegisterCandidateRequest(
            "Ana Ferreira",
            "ana.ferreira@exemplo.pt",
            "912345678",
            Guimaraes,
            ValidPassword,
            AcceptTerms: true);
    }

    private async Task<IReadOnlyList<FieldErrorDto>> RegisterExpectingErrorsAsync(RegisterCandidateRequest request)
    {
        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.RegisterCandidateAsync(request, TestContext.Current.CancellationToken));
        return exception.Fields;
    }

    private sealed class FakeUsers : IUserRepository
    {
        public List<string> Existing { get; } = [];

        public List<AppUser> Added { get; } = [];

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Added.SingleOrDefault(user => user.Id == id));
        }

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                this.Added.SingleOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Existing.Contains(email, StringComparer.OrdinalIgnoreCase));
        }

        public Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
        {
            this.Added.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCandidates : ICandidateRepository
    {
        public List<Candidate> Added { get; } = [];

        public Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
        {
            this.Added.Add(candidate);
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
        private readonly Guid locationId;

        public FakeReferenceData(Guid locationId)
        {
            this.locationId = locationId;
        }

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<Location>());
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == this.locationId);
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
            return Task.FromResult(2);
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
