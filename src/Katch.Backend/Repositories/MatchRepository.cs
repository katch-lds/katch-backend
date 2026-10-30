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
    public Task<List<Match>> ListMatchesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return this.context.Matches
            .Where(match => match.Status == MatchStatus.Matched
                && this.context.Jobs.Any(job => job.Id == match.JobId && job.CompanyId == companyId))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<Match>> ListOpenConversationsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // A parte do lado da Empresa é o Recrutador que a registou (company.recruiter_id).
        return this.context.Matches
            .Where(match => match.Status == MatchStatus.Matched
                && match.ConversationStatus == ConversationStatus.Open
                && (match.CandidateId == userId
                    || this.context.Jobs.Any(job => job.Id == match.JobId
                        && this.context.Companies.Any(company => company.Id == job.CompanyId && company.RecruiterId == userId))))
            .ToListAsync(cancellationToken);
    }
}
