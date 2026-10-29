// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
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
    public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Jobs
            .Include(job => job.Skills)
            .Include(job => job.Benefits)
            .FirstOrDefaultAsync(job => job.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        await this.context.Jobs.AddAsync(job, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<Job>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var jobs = await this.context.Jobs
            .AsNoTracking()
            .Include(job => job.Skills)
            .Include(job => job.Benefits)
            .Where(job => job.CompanyId == companyId)
            .Select(job => new
            {
                Job = job,
                CreatedAt = this.context.OperationLogs
                    .Where(log => log.EntityId == job.Id && log.Operation == OperationType.JobCreated)
                    .OrderBy(log => log.Id)
                    .Select(log => (DateTimeOffset?)log.OccurredAt)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // A ordenação é feita aqui porque o SQLite dos testes não ordena por DateTimeOffset; as listas não têm
        // paginação e têm a dimensão das vagas de uma Empresa (L-02).
        return jobs
            .OrderByDescending(item => item.CreatedAt ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.Job.Id)
            .Select(item => item.Job)
            .ToList();
    }

    /// <inheritdoc/>
    public Task<bool> HasInterestsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return this.context.Matches.AnyAsync(
            match => match.JobId == jobId && match.Status != MatchStatus.DeclinedByCandidate,
            cancellationToken);
    }
}
