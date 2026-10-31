// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="ICompanyAdminService"/> (modelo de classes do backend v02, secções 5.1 e 5.3).
/// </summary>
/// <remarks>
/// As transições de estado são as da <see cref="Company"/> (documentação da API v02, secção 6.11): o serviço verifica-as
/// antes, para responder com o ERR-30 e o estado atual. A mudança de estado da Empresa e a linha do <c>operation_log</c>
/// (RF112) são gravadas numa única gravação, que é uma transação. A validação do motivo da recusa vem antes de carregar a
/// Empresa, para que um pedido inválido não dependa do estado.
/// </remarks>
public sealed class CompanyAdminService : ICompanyAdminService
{
    private readonly ICompanyRepository companies;
    private readonly IReferenceDataRepository referenceData;
    private readonly IOperationLogger operationLogger;
    private readonly IUnitOfWork unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyAdminService"/> class.
    /// </summary>
    /// <param name="companies">Repositório das Empresas.</param>
    /// <param name="referenceData">Repositório das listas pré-definidas, para a designação da localidade.</param>
    /// <param name="operationLogger">Registo das operações do RF112.</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    public CompanyAdminService(
        ICompanyRepository companies,
        IReferenceDataRepository referenceData,
        IOperationLogger operationLogger,
        IUnitOfWork unitOfWork)
    {
        this.companies = companies;
        this.referenceData = referenceData;
        this.operationLogger = operationLogger;
        this.unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<List<PendingCompanyDto>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        List<Company> pending = await this.companies.ListPendingAsync(cancellationToken);

        // A ordenação é feita aqui, como nas restantes listas.
        return pending
            .Select(company => new PendingCompanyDto(company.Id, company.CompanyName, company.TaxId, company.SubmittedAt))
            .OrderBy(item => item.SubmittedAt)
            .ThenBy(item => item.CompanyId)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<CompanyRegistrationDto> GetSubmittedDataAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        Company company = await this.LoadAsync(companyId, cancellationToken);
        Location? location = (await this.referenceData.ListLocationsAsync(cancellationToken))
            .SingleOrDefault(item => item.Id == company.LocationId);

        return new CompanyRegistrationDto(
            company.Id,
            company.CompanyName,
            company.TaxId,
            company.Industry,
            company.Address,
            company.LocationId,
            location is null ? string.Empty : ReferenceListService.DisplayName(location),
            company.ContactEmail,
            company.ContactPhone,
            company.ResponsibleName,
            company.SubmittedAt);
    }

    /// <inheritdoc/>
    public async Task ApproveAsync(Guid adminId, Guid companyId, CancellationToken cancellationToken = default)
    {
        Company company = await this.LoadAsync(companyId, cancellationToken);
        EnsureStatus(company, CompanyStatus.Pending);

        company.Approve();

        await this.operationLogger.LogAsync(adminId, OperationType.CompanyApproved, company.Id, cancellationToken);
        await this.SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RejectAsync(
        Guid adminId,
        Guid companyId,
        RejectCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string reason = ValidateReason(request.Reason);
        Company company = await this.LoadAsync(companyId, cancellationToken);
        EnsureStatus(company, CompanyStatus.Pending);

        company.Reject(reason);

        await this.operationLogger.LogAsync(adminId, OperationType.CompanyRejected, company.Id, cancellationToken);
        await this.SaveAsync(cancellationToken);
    }

    private static string ValidateReason(string? value)
    {
        string reason = value?.Trim() ?? string.Empty;

        if (reason.Length == 0)
        {
            throw new ValidationFailedException([new FieldErrorDto("reason", FieldReasons.Required)]);
        }

        if (reason.Length < Company.MinReasonLength || reason.Length > Company.MaxReasonLength)
        {
            throw new ValidationFailedException([new FieldErrorDto("reason", FieldReasons.RejectionReasonLength)]);
        }

        return reason;
    }

    private static void EnsureStatus(Company company, CompanyStatus expected)
    {
        if (company.Status != expected)
        {
            throw new OperationConflictException(
                ApiErrorResponse.InvalidState(ApiErrorResponse.CompanyStatusLabel(company.Status)));
        }
    }

    private async Task<Company> LoadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return await this.companies.GetByIdAsync(companyId, cancellationToken)
            ?? throw new ElementNotFoundException();
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // A Empresa foi alterada por outro pedido entre a leitura e a gravação: a mudança de estado e o registo da
            // operação, que fazem parte da mesma unidade de trabalho, não ficam gravados e o Administrador repete a
            // operação (modelo de dados, secção 7.3).
            throw new OperationConflictException(ApiErrorResponse.ConcurrencyConflict());
        }
    }
}
