// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="IMatchRepository"/> sobre o <see cref="KatchDbContext"/>.
/// </summary>
public sealed class MatchRepository : IMatchRepository
{
    private readonly KatchDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchRepository"/> class.
    /// </summary>
    /// <param name="context">Contexto da base de dados do pedido.</param>
    public MatchRepository(KatchDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
    {
        return this.context.Matches
            .FirstOrDefaultAsync(match => match.CandidateId == candidateId && match.JobId == jobId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Match match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);

        await this.context.Matches.AddAsync(match, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> HasInterestFromCandidateInCompanyAsync(Guid candidateId, Guid recruiterId, CancellationToken cancellationToken = default)
    {
        return (from match in this.context.Matches
                join job in this.context.Jobs on match.JobId equals job.Id
                join company in this.context.Companies on job.CompanyId equals company.Id
                where match.CandidateId == candidateId
                    && match.Status != MatchStatus.DeclinedByCandidate
                    && company.RecruiterId == recruiterId
                select match.Id)
            .AnyAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Dictionary<Guid, int>> CountWaitingByJobAsync(
        IReadOnlyCollection<Guid> jobIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobIds);

        return this.context.Matches
            .Where(match => jobIds.Contains(match.JobId) && match.Status == MatchStatus.Waiting)
            .GroupBy(match => match.JobId)
            .Select(group => new { JobId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.JobId, item => item.Count, cancellationToken);
    }
}
