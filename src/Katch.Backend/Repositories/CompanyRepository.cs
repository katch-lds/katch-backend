// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
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
}
