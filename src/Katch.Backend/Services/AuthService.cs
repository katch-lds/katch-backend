// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IAuthService"/> (modelo de classes do backend v02, secções 5.1 e 5.3).
/// </summary>
public sealed class AuthService : IAuthService
{
    /// <summary>
    /// Nome do índice único sobre <c>lower(email)</c> (modelo de dados v02, secção 5.3).
    /// </summary>
    internal const string EmailUniqueIndex = "uq_app_user_email";

    /// <summary>
    /// Resumo de uma palavra-passe aleatória que ninguém conhece, calculado com o mesmo <see cref="IPasswordHasher"/> das
    /// contas. Quando o correio não existe, o início de sessão verifica a palavra-passe contra este resumo, para que o
    /// tempo de resposta não revele se o correio está registado (RNF009).
    /// </summary>
    internal static readonly string ReferenceHash = new IdentityPasswordHasher().Hash(Guid.NewGuid().ToString("N"));

    private const int MaxFullNameLength = 150;
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;

    private readonly IUserRepository users;
    private readonly ICandidateRepository candidates;
    private readonly IReferenceDataRepository referenceData;
    private readonly IUnitOfWork unitOfWork;
    private readonly IPasswordHasher passwordHasher;
    private readonly ITokenService tokenService;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthService"/> class.
    /// </summary>
    /// <param name="users">Repositório das contas.</param>
    /// <param name="candidates">Repositório dos Candidatos.</param>
    /// <param name="referenceData">Repositório das listas pré-definidas, para confirmar a localidade.</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    /// <param name="passwordHasher">Cálculo do resumo da palavra-passe.</param>
    /// <param name="tokenService">Emissão da credencial de sessão.</param>
    /// <param name="clock">Relógio do instante do registo.</param>
    public AuthService(
        IUserRepository users,
        ICandidateRepository candidates,
        IReferenceDataRepository referenceData,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IClock clock)
    {
        this.users = users;
        this.candidates = candidates;
        this.referenceData = referenceData;
        this.unitOfWork = unitOfWork;
        this.passwordHasher = passwordHasher;
        this.tokenService = tokenService;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public async Task<LoginResponse> RegisterCandidateAsync(
        RegisterCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Os espaços no início e no fim são retirados antes da validação (documentação da API v02, secção 2.3).
        string fullName = request.FullName?.Trim() ?? string.Empty;
        string email = request.Email?.Trim() ?? string.Empty;
        string phoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
        string password = request.Password ?? string.Empty;

        var errors = new List<FieldErrorDto>();
        ValidateFullName(fullName, errors);
        await this.ValidateEmailAsync(email, errors, cancellationToken);
        ValidatePhoneNumber(phoneNumber, errors);
        await this.ValidateLocationAsync(request.LocationId, errors, cancellationToken);
        ValidatePassword(password, errors, "password");

        if (!request.AcceptTerms)
        {
            errors.Add(new FieldErrorDto("acceptTerms", FieldReasons.AcceptTerms));
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        DateTimeOffset now = this.clock.UtcNow;
        var user = new AppUser(email, this.passwordHasher.Hash(password), UserType.Candidate, termsAcceptedAt: now, now);
        var candidate = new Candidate(user, fullName, phoneNumber, request.LocationId);

        await this.users.AddAsync(user, cancellationToken);
        await this.candidates.AddAsync(candidate, cancellationToken);
        await this.SaveAccountAsync(cancellationToken);

        return this.tokenService.CreateToken(user);
    }

    /// <inheritdoc/>
    public async Task<LoginResponse> CreateRecruiterAccountAsync(
        CreateRecruiterAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Os espaços no início e no fim são retirados antes da validação (documentação da API v02, secção 2.3).
        string email = request.Email?.Trim() ?? string.Empty;
        string password = request.Password ?? string.Empty;

        var errors = new List<FieldErrorDto>();
        await this.ValidateEmailAsync(email, errors, cancellationToken);
        ValidatePassword(password, errors, "password");

        if (!request.AcceptTerms)
        {
            errors.Add(new FieldErrorDto("acceptTerms", FieldReasons.AcceptTerms));
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        // A conta fica ativa e sem Empresa; as operações ApprovedCompany devolvem ERR-07 até à aprovação (RF037, RF039).
        DateTimeOffset now = this.clock.UtcNow;
        var user = new AppUser(email, this.passwordHasher.Hash(password), UserType.Recruiter, termsAcceptedAt: now, now);
        var recruiter = new Recruiter(user);

        await this.users.AddAsync(user, cancellationToken);
        await this.users.AddRecruiterAsync(recruiter, cancellationToken);
        await this.SaveAccountAsync(cancellationToken);

        return this.tokenService.CreateToken(user);
    }

    /// <inheritdoc/>
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        ClientApp app,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Os espaços do correio são retirados (documentação da API v02, secção 2.3); a palavra-passe segue como foi enviada.
        string email = request.Email?.Trim() ?? string.Empty;
        string password = request.Password ?? string.Empty;

        // Só se verifica que os campos não estão vazios: o formato do correio e a política da palavra-passe não são
        // revelados (RNF009).
        var errors = new List<FieldErrorDto>();
        if (email.Length == 0)
        {
            errors.Add(new FieldErrorDto("email", FieldReasons.Required));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add(new FieldErrorDto("password", FieldReasons.Required));
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        // 1.º As credenciais. A palavra-passe é verificada mesmo sem conta, e a recusa é a mesma nos dois casos.
        AppUser? user = await this.users.GetByEmailAsync(email, cancellationToken);
        bool passwordMatches = this.passwordHasher.Verify(user?.PasswordHash ?? ReferenceHash, password);
        if (user is null || !passwordMatches)
        {
            throw new LoginRejectedException(LoginRejection.InvalidCredentials);
        }

        // 2.º O estado da conta, que só é revelado a quem apresentou a palavra-passe correta.
        if (!user.CanSignIn())
        {
            throw new LoginRejectedException(
                user.Status == AccountStatus.Suspended ? LoginRejection.AccountSuspended : LoginRejection.AccountBlocked);
        }

        // 3.º O ponto de acesso. A Empresa do Recrutador não é consultada: só vedam as operações ApprovedCompany (RF038).
        if (!CanSignInFrom(app, user.UserType))
        {
            throw new LoginRejectedException(LoginRejection.WrongAccessPoint);
        }

        return this.tokenService.CreateToken(user);
    }

    /// <inheritdoc/>
    public async Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string currentPassword = request.CurrentPassword ?? string.Empty;
        string newPassword = request.NewPassword ?? string.Empty;

        // A conta é a da credencial de sessão, cujo estado o pedido já confirmou na base de dados.
        AppUser user = await this.users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("A conta da credencial de sessão não existe.");

        var errors = new List<FieldErrorDto>();
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            errors.Add(new FieldErrorDto("currentPassword", FieldReasons.Required));
        }
        else if (!this.passwordHasher.Verify(user.PasswordHash, currentPassword))
        {
            errors.Add(new FieldErrorDto("currentPassword", FieldReasons.CurrentIncorrect));
        }

        ValidatePassword(newPassword, errors, "newPassword");

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        user.SetPasswordHash(this.passwordHasher.Hash(newPassword), this.clock.UtcNow);
        await this.unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A aplicação móvel é só do Candidato; a área de gestão web é só do Recrutador e do Administrador (documentação da API
    /// v02, secção 3.2). Qualquer outra combinação é recusada.
    /// </summary>
    private static bool CanSignInFrom(ClientApp app, UserType userType)
    {
        return (app, userType) switch
        {
            (ClientApp.Mobile, UserType.Candidate) => true,
            (ClientApp.Web, UserType.Recruiter or UserType.Admin) => true,
            _ => false,
        };
    }

    private static void ValidateFullName(string fullName, List<FieldErrorDto> errors)
    {
        if (fullName.Length == 0)
        {
            errors.Add(new FieldErrorDto("fullName", FieldReasons.Required));
        }
        else if (fullName.Length > MaxFullNameLength)
        {
            errors.Add(new FieldErrorDto("fullName", FieldReasons.FullNameTooLong));
        }
    }

    private static void ValidatePhoneNumber(string phoneNumber, List<FieldErrorDto> errors)
    {
        if (phoneNumber.Length == 0)
        {
            errors.Add(new FieldErrorDto("phoneNumber", FieldReasons.Required));
        }
        else if (!FieldFormats.IsNineDigits(phoneNumber))
        {
            errors.Add(new FieldErrorDto("phoneNumber", FieldReasons.PhoneNumberFormat));
        }
    }

    private static void ValidatePassword(string password, List<FieldErrorDto> errors, string field)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.Required));
        }
        else if (password.Length > MaxPasswordLength)
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.PasswordTooLong));
        }
        else if (password.Length < MinPasswordLength || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.PasswordPolicy));
        }
    }

    private async Task ValidateEmailAsync(string email, List<FieldErrorDto> errors, CancellationToken cancellationToken)
    {
        if (email.Length == 0)
        {
            errors.Add(new FieldErrorDto("email", FieldReasons.Required));
        }
        else if (email.Length > AppUser.MaxLength)
        {
            errors.Add(new FieldErrorDto("email", FieldReasons.EmailTooLong));
        }
        else if (!FieldFormats.IsEmail(email))
        {
            errors.Add(new FieldErrorDto("email", FieldReasons.EmailFormat));
        }
        else if (await this.users.EmailExistsAsync(email, cancellationToken))
        {
            errors.Add(new FieldErrorDto("email", FieldReasons.EmailTaken));
        }
    }

    /// <summary>
    /// Grava a conta nova. Se outro pedido registou o mesmo correio entre a verificação e a gravação, o índice único
    /// <c>uq_app_user_email</c> rejeita a segunda e a resposta é o mesmo ERR-10 do campo <c>email</c>.
    /// </summary>
    private async Task SaveAccountAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == EmailUniqueIndex)
        {
            throw new ValidationFailedException([new FieldErrorDto("email", FieldReasons.EmailTaken)]);
        }
    }

    private async Task ValidateLocationAsync(Guid locationId, List<FieldErrorDto> errors, CancellationToken cancellationToken)
    {
        if (locationId == Guid.Empty)
        {
            errors.Add(new FieldErrorDto("locationId", FieldReasons.Required));
        }
        else if (!await this.referenceData.LocationExistsAsync(locationId, cancellationToken))
        {
            errors.Add(new FieldErrorDto("locationId", FieldReasons.NotInList));
        }
    }
}
