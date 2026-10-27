// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="ICompanyService"/> (modelo de classes do backend v02, secções 5.1 e 5.3).
/// </summary>
/// <remarks>
/// A validação automática junta todos os campos em erro na mesma resposta (RF042) e repete as regras do modelo de dados
/// v02 (secções 6.10 e 7.1), que a <see cref="Company"/> e os <c>CHECK</c> da tabela voltam a garantir. A unicidade do
/// NIF e do Recrutador depende de outras linhas: é verificada antes de gravar e, se dois pedidos passarem a verificação
/// ao mesmo tempo, os índices <c>uq_company_tax_id</c> e <c>uq_company_recruiter</c> rejeitam o segundo com a mesma
/// resposta. A Empresa e a linha do <c>operation_log</c> são gravadas numa única gravação.
/// </remarks>
public sealed class CompanyService : ICompanyService
{
    /// <summary>
    /// Nome do índice único sobre <c>company.tax_id</c> (modelo de dados v02, secção 5.2).
    /// </summary>
    internal const string TaxIdUniqueIndex = "uq_company_tax_id";

    /// <summary>
    /// Nome do índice único sobre <c>company.recruiter_id</c> (modelo de dados v02, secção 5.2).
    /// </summary>
    internal const string RecruiterUniqueIndex = "uq_company_recruiter";

    private const int MaxCompanyNameLength = 200;
    private const int MaxAddressLength = 255;
    private const int MaxContactEmailLength = 255;
    private const int MaxResponsibleNameLength = 150;

    private readonly ICompanyRepository companies;
    private readonly IReferenceDataRepository referenceData;
    private readonly IOperationLogger operationLogger;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyService"/> class.
    /// </summary>
    /// <param name="companies">Repositório das Empresas.</param>
    /// <param name="referenceData">Repositório das listas pré-definidas, para confirmar a localidade.</param>
    /// <param name="operationLogger">Registo das operações do RF112.</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    /// <param name="clock">Relógio do instante da submissão.</param>
    public CompanyService(
        ICompanyRepository companies,
        IReferenceDataRepository referenceData,
        IOperationLogger operationLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        this.companies = companies;
        this.referenceData = referenceData;
        this.operationLogger = operationLogger;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public async Task<CompanyStatusDto> RegisterAsync(
        Guid recruiterId,
        CompanyRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Um Recrutador representa uma única Empresa (especificação de requisitos v02, secção 2.2).
        if (await this.companies.GetByRecruiterAsync(recruiterId, cancellationToken) is not null)
        {
            throw new OperationConflictException(ApiErrorResponse.CompanyAlreadyRegistered());
        }

        CompanyRegistration data = await this.ValidateAsync(request, exceptCompanyId: null, cancellationToken);
        var company = new Company(recruiterId, data, this.clock.UtcNow);

        await this.companies.AddAsync(company, cancellationToken);
        await this.operationLogger.LogAsync(recruiterId, OperationType.CompanySubmitted, company.Id, cancellationToken);
        await this.SaveAsync(cancellationToken);

        return ToStatus(company);
    }

    /// <inheritdoc/>
    public async Task<CompanyStatusDto> ResubmitAsync(
        Guid recruiterId,
        CompanyRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Company company = await this.companies.GetByRecruiterAsync(recruiterId, cancellationToken)
            ?? throw new ElementNotFoundException();

        // Só um pedido recusado pode ser corrigido e submetido de novo (RF045).
        if (company.Status != CompanyStatus.Rejected)
        {
            throw new OperationConflictException(
                ApiErrorResponse.InvalidState(ApiErrorResponse.CompanyStatusLabel(company.Status)));
        }

        // O NIF pode ser corrigido, porque a Empresa nunca foi aprovada; só não pode ser o de outra Empresa.
        CompanyRegistration data = await this.ValidateAsync(request, company.Id, cancellationToken);
        company.Resubmit(data, this.clock.UtcNow);

        await this.operationLogger.LogAsync(recruiterId, OperationType.CompanySubmitted, company.Id, cancellationToken);
        await this.SaveAsync(cancellationToken);

        return ToStatus(company);
    }

    /// <inheritdoc/>
    public async Task<CompanyStatusDto> GetStatusAsync(Guid recruiterId, CancellationToken cancellationToken = default)
    {
        Company company = await this.companies.GetByRecruiterAsync(recruiterId, cancellationToken)
            ?? throw new ElementNotFoundException();

        return ToStatus(company);
    }

    private static CompanyStatusDto ToStatus(Company company)
    {
        // O motivo só existe no estado recusado (ck_company_reason); nos restantes vai sempre null.
        string? reason = company.Status == CompanyStatus.Rejected ? company.StatusReason : null;
        return new CompanyStatusDto(company.Id, company.Status, reason, company.SubmittedAt);
    }

    private static void ValidateText(string field, string value, int maxLength, List<FieldErrorDto> errors)
    {
        if (value.Length == 0)
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.Required));
        }
        else if (value.Length > maxLength)
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.TextTooLong(maxLength)));
        }
    }

    private static void ValidateIndustry(Industry? industry, List<FieldErrorDto> errors)
    {
        if (industry is null)
        {
            errors.Add(new FieldErrorDto("industry", FieldReasons.Required));
        }
        else if (!Enum.IsDefined(industry.Value))
        {
            errors.Add(new FieldErrorDto("industry", FieldReasons.NotInList));
        }
    }

    private static void ValidateContactEmail(string contactEmail, List<FieldErrorDto> errors)
    {
        if (contactEmail.Length == 0)
        {
            errors.Add(new FieldErrorDto("contactEmail", FieldReasons.Required));
        }
        else if (contactEmail.Length > MaxContactEmailLength)
        {
            errors.Add(new FieldErrorDto("contactEmail", FieldReasons.EmailTooLong));
        }
        else if (!FieldFormats.IsEmail(contactEmail))
        {
            errors.Add(new FieldErrorDto("contactEmail", FieldReasons.EmailFormat));
        }
    }

    private static void ValidateContactPhone(string contactPhone, List<FieldErrorDto> errors)
    {
        if (contactPhone.Length == 0)
        {
            errors.Add(new FieldErrorDto("contactPhone", FieldReasons.Required));
        }
        else if (!FieldFormats.IsNineDigits(contactPhone))
        {
            errors.Add(new FieldErrorDto("contactPhone", FieldReasons.PhoneNumberFormat));
        }
    }

    /// <summary>
    /// Valida todos os campos no mesmo pedido (RF042), pela ordem do <see cref="CompanyRegistrationRequest"/>, e devolve
    /// os dados de registo sem os espaços nas extremidades (documentação da API v02, secção 2.3).
    /// </summary>
    private async Task<CompanyRegistration> ValidateAsync(
        CompanyRegistrationRequest request,
        Guid? exceptCompanyId,
        CancellationToken cancellationToken)
    {
        string companyName = request.CompanyName?.Trim() ?? string.Empty;
        string taxId = request.TaxId?.Trim() ?? string.Empty;
        string address = request.Address?.Trim() ?? string.Empty;
        string contactEmail = request.ContactEmail?.Trim() ?? string.Empty;
        string contactPhone = request.ContactPhone?.Trim() ?? string.Empty;
        string responsibleName = request.ResponsibleName?.Trim() ?? string.Empty;

        var errors = new List<FieldErrorDto>();
        ValidateText("companyName", companyName, MaxCompanyNameLength, errors);
        await this.ValidateTaxIdAsync(taxId, exceptCompanyId, errors, cancellationToken);
        ValidateIndustry(request.Industry, errors);
        ValidateText("address", address, MaxAddressLength, errors);
        await this.ValidateLocationAsync(request.LocationId, errors, cancellationToken);
        ValidateContactEmail(contactEmail, errors);
        ValidateContactPhone(contactPhone, errors);
        ValidateText("responsibleName", responsibleName, MaxResponsibleNameLength, errors);

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        return new CompanyRegistration
        {
            CompanyName = companyName,
            TaxId = taxId,
            Industry = request.Industry!.Value,
            Address = address,
            LocationId = request.LocationId,
            ContactEmail = contactEmail,
            ContactPhone = contactPhone,
            ResponsibleName = responsibleName,
        };
    }

    private async Task ValidateTaxIdAsync(
        string taxId,
        Guid? exceptCompanyId,
        List<FieldErrorDto> errors,
        CancellationToken cancellationToken)
    {
        if (taxId.Length == 0)
        {
            errors.Add(new FieldErrorDto("taxId", FieldReasons.Required));
        }
        else if (!FieldFormats.IsNineDigits(taxId))
        {
            errors.Add(new FieldErrorDto("taxId", FieldReasons.TaxIdFormat));
        }
        else if (await this.companies.TaxIdExistsAsync(taxId, exceptCompanyId, cancellationToken))
        {
            errors.Add(new FieldErrorDto("taxId", FieldReasons.TaxIdTaken));
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

    /// <summary>
    /// Grava a Empresa e a linha do <c>operation_log</c>. Se outro pedido gravou o mesmo NIF, ou outra Empresa do mesmo
    /// Recrutador, entre a verificação e a gravação, a resposta é a mesma da verificação.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == TaxIdUniqueIndex)
        {
            throw new ValidationFailedException([new FieldErrorDto("taxId", FieldReasons.TaxIdTaken)]);
        }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == RecruiterUniqueIndex)
        {
            throw new OperationConflictException(ApiErrorResponse.CompanyAlreadyRegistered());
        }
    }
}
