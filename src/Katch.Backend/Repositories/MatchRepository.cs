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
    public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Matches.FirstOrDefaultAsync(match => match.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<WaitingInterest>> ListWaitingByJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        List<WaitingInterest> waiting = await (
                from match in this.context.Matches.AsNoTracking()
                join candidate in this.context.Candidates.AsNoTracking() on match.CandidateId equals candidate.Id
                join location in this.context.Locations.AsNoTracking() on candidate.LocationId equals location.Id
                where match.JobId == jobId && match.Status == MatchStatus.Waiting
                select new WaitingInterest(
                    match.Id,
                    candidate.Id,
                    candidate.FullName,
                    candidate.DesiredRole,
                    location,
                    match.CandidateActionAt))
            .ToListAsync(cancellationToken);

        // A ordenação é feita depois da leitura porque o SQLite dos testes de integração guarda o DateTimeOffset como
        // texto e o EF Core não a traduz; as listas são curtas (sem paginação, documentação da API v02, L-02).
        return waiting.OrderBy(interest => interest.InterestAt).ToList();
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
}
