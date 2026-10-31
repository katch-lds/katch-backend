// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="IJobRepository"/> sobre o <see cref="KatchDbContext"/>.
/// </summary>
public sealed class JobRepository : IJobRepository
{
    private readonly KatchDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobRepository"/> class.
    /// </summary>
    /// <param name="context">Contexto da base de dados do pedido.</param>
    public JobRepository(KatchDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public Task<JobWithCompanyStatus?> GetWithCompanyStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // O filtro global das vagas exclui as eliminadas (DM-09).
        return (from job in this.context.Jobs.AsNoTracking()
                join company in this.context.Companies.AsNoTracking() on job.CompanyId equals company.Id
                where job.Id == jobId
                select new JobWithCompanyStatus(job, company.Status))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> CountFirstPublishedAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
    {
        // Sem o filtro global das vagas eliminadas (DM-09): a primeira publicação aconteceu no período.
        return this.context.Jobs
            .IgnoreQueryFilters()
            .CountAsync(job => job.FirstPublishedAt >= periodStart && job.FirstPublishedAt < periodEnd, cancellationToken);
    }
}
