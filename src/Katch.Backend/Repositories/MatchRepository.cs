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
    public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Matches.FirstOrDefaultAsync(match => match.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Guid?> GetRecruiterIdAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        // A parte do lado da Empresa é o Recrutador que a registou (company.recruiter_id).
        return await (from match in this.context.Matches
                      join job in this.context.Jobs on match.JobId equals job.Id
                      join company in this.context.Companies on job.CompanyId equals company.Id
                      where match.Id == matchId
                      select (Guid?)company.RecruiterId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<MatchView>> ListMatchViewsByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        return this.QueryMatchViews().Where(view => view.Match.CandidateId == candidateId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<MatchView>> ListMatchViewsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return this.QueryMatchViews()
            .Where(view => this.context.Jobs.Any(job => job.Id == view.Match.JobId && job.CompanyId == companyId))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<ConversationView>> ListConversationsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return (from match in this.context.Matches.AsNoTracking()
                join job in this.context.Jobs on match.JobId equals job.Id
                join company in this.context.Companies on job.CompanyId equals company.Id
                join candidate in this.context.Candidates on match.CandidateId equals candidate.Id
                where match.Status == MatchStatus.Matched
                    && (match.CandidateId == userId || company.RecruiterId == userId)
                select new ConversationView(
                    match,
                    match.CandidateId == userId ? company.ResponsibleName : candidate.FullName,
                    job.Title,
                    company.CompanyName,
                    this.context.Messages.Where(message => message.MatchId == match.Id).Max(message => (DateTimeOffset?)message.SentAt),
                    this.context.Messages.Count(message => message.MatchId == match.Id && message.SenderId != userId && message.ReadAt == null)))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<MatchView> QueryMatchViews()
    {
        return from match in this.context.Matches.AsNoTracking()
               join job in this.context.Jobs on match.JobId equals job.Id
               join company in this.context.Companies on job.CompanyId equals company.Id
               join location in this.context.Locations on job.LocationId equals location.Id
               join candidate in this.context.Candidates on match.CandidateId equals candidate.Id
               where match.Status == MatchStatus.Matched
               select new MatchView(
                   match,
                   job.Title,
                   job.Status,
                   location,
                   company.CompanyName,
                   company.LogoPath,
                   company.ResponsibleName,
                   company.ContactEmail,
                   company.ContactPhone,
                   candidate.FullName,
                   candidate.User.Email,
                   candidate.PhoneNumber);
    }
}
