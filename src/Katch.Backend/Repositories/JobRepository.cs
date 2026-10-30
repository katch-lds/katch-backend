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
        // O operation_log só recebe inserções e o seu identificador é sequencial, por isso a ordem do registo
        // JOB_CREATED é a da data de criação (DM-22) e ordena na base de dados em qualquer fornecedor, sem ordenar por
        // DateTimeOffset. As vagas sem esse registo (inseridas por SQL) ficam no fim.
        return await this.context.Jobs
            .AsNoTracking()
            .Include(job => job.Skills)
            .Include(job => job.Benefits)
            .Where(job => job.CompanyId == companyId)
            .OrderByDescending(job => this.context.OperationLogs
                .Where(log => log.EntityId == job.Id && log.Operation == OperationType.JobCreated)
                .Min(log => (long?)log.Id) ?? 0)
            .ThenBy(job => job.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> HasInterestsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return this.context.Matches.AnyAsync(
            match => match.JobId == jobId && match.Status != MatchStatus.DeclinedByCandidate,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<Job>> ListExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // O filtro global exclui as vagas eliminadas (DM-09); no PostgreSQL, a consulta usa o índice ix_job_expiry, só
        // das vagas publicadas. As datas são timestamptz, por isso a comparação é entre instantes, sem fuso horário.
        return this.context.Jobs
            .Where(job => job.Status == JobStatus.Published && job.ExpiresAt != null && job.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
    }
}
