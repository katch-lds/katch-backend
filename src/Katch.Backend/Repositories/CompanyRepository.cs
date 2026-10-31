// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="ICompanyRepository"/> sobre o <see cref="KatchDbContext"/>.
/// </summary>
public sealed class CompanyRepository : ICompanyRepository
{
    private readonly KatchDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyRepository"/> class.
    /// </summary>
    /// <param name="context">Contexto da base de dados do pedido.</param>
    public CompanyRepository(KatchDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
    {
        return this.context.Companies.SingleOrDefaultAsync(company => company.RecruiterId == recruiterId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxId);

        return this.context.Companies.AnyAsync(
            company => company.TaxId == taxId && (exceptId == null || company.Id != exceptId),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);

        await this.context.Companies.AddAsync(company, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> CountByStatusAtAsync(CompanyStatus status, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        OperationType[] statusOperations =
        [
            OperationType.CompanySubmitted,
            OperationType.CompanyApproved,
            OperationType.CompanyRejected,
            OperationType.CompanySuspended,
            OperationType.CompanyReactivated,
        ];
        OperationType[] leavingStatus = OperationsLeaving(status);

        // A data de submissão muda numa nova submissão (RF045); a primeira fica no registo. Sem registo (dados inseridos
        // por SQL), vale a data de submissão.
        return this.context.Companies
            .Where(company => company.SubmittedAt < at
                || this.context.OperationLogs.Any(log => log.EntityId == company.Id
                    && log.Operation == OperationType.CompanySubmitted
                    && log.OccurredAt < at))
            .Select(company => new
            {
                company.Status,
                NextOperation = this.context.OperationLogs
                    .Where(log => log.EntityId == company.Id
                        && statusOperations.Contains(log.Operation)
                        && log.OccurredAt >= at)
                    .OrderBy(log => log.OccurredAt)
                    .ThenBy(log => log.Id)
                    .Select(log => (OperationType?)log.Operation)
                    .FirstOrDefault(),
            })
            .CountAsync(
                company => company.NextOperation == null
                    ? company.Status == status
                    : leavingStatus.Contains(company.NextOperation.Value),
                cancellationToken);
    }

    /// <summary>
    /// Operações cujo estado anterior é o indicado. Cada operação tem um único estado anterior (diagrama de estados da
    /// Empresa): a aprovação e a recusa partem de pendente, a suspensão de aprovada, a reativação de suspensa e a nova
    /// submissão de recusada.
    /// </summary>
    private static OperationType[] OperationsLeaving(CompanyStatus status)
    {
        return status switch
        {
            CompanyStatus.Pending => [OperationType.CompanyApproved, OperationType.CompanyRejected],
            CompanyStatus.Approved => [OperationType.CompanySuspended],
            CompanyStatus.Rejected => [OperationType.CompanySubmitted],
            CompanyStatus.Suspended => [OperationType.CompanyReactivated],
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Estado da Empresa desconhecido."),
        };
    }
}
