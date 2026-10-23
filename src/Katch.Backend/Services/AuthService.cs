// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.RegularExpressions;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IAuthService"/> (modelo de classes do backend v02, secções 5.1 e 5.3).
/// </summary>
public sealed partial class AuthService : IAuthService
{
    /// <summary>
    /// Nome do índice único sobre <c>lower(email)</c> (modelo de dados v02, secção 5.3).
    /// </summary>
    internal const string EmailUniqueIndex = "uq_app_user_email";

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
        ValidatePassword(password, errors);

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

        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == EmailUniqueIndex)
        {
            // Outro pedido registou o mesmo correio entre a verificação e a gravação.
            throw new ValidationFailedException([new FieldErrorDto("email", FieldReasons.EmailTaken)]);
        }

        return this.tokenService.CreateToken(user);
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
        else if (!PhoneNumberPattern().IsMatch(phoneNumber))
        {
            errors.Add(new FieldErrorDto("phoneNumber", FieldReasons.PhoneNumberFormat));
        }
    }

    private static void ValidatePassword(string password, List<FieldErrorDto> errors)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add(new FieldErrorDto("password", FieldReasons.Required));
        }
        else if (password.Length > MaxPasswordLength)
        {
            errors.Add(new FieldErrorDto("password", FieldReasons.PasswordTooLong));
        }
        else if (password.Length < MinPasswordLength || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            errors.Add(new FieldErrorDto("password", FieldReasons.PasswordPolicy));
        }
    }

    /// <summary>
    /// Mesma regra do <c>CHECK ck_app_user_email</c> e do <see cref="AppUser"/>: exatamente um <c>@</c>, com texto sem
    /// espaços de cada lado (P04).
    /// </summary>
    private static bool HasEmailFormat(string email)
    {
        int at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0
            && at == email.LastIndexOf('@')
            && at < email.Length - 1
            && !email.Any(char.IsWhiteSpace);
    }

    [GeneratedRegex(@"^[0-9]{9}\z")]
    private static partial Regex PhoneNumberPattern();

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
        else if (!HasEmailFormat(email))
        {
            errors.Add(new FieldErrorDto("email", FieldReasons.EmailFormat));
        }
        else if (await this.users.EmailExistsAsync(email, cancellationToken))
        {
            errors.Add(new FieldErrorDto("email", FieldReasons.EmailTaken));
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
