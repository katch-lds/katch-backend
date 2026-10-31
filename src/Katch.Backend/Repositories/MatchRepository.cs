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
    public Task<int> CountInterestsAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
    {
        return this.context.Matches.CountAsync(
            match => match.Status != MatchStatus.DeclinedByCandidate
                && match.CandidateActionAt >= periodStart
                && match.CandidateActionAt < periodEnd,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> CountMatchesAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
    {
        return this.context.Matches.CountAsync(
            match => match.Status == MatchStatus.Matched
                && match.RecruiterActionAt >= periodStart
                && match.RecruiterActionAt < periodEnd,
            cancellationToken);
    }
}
