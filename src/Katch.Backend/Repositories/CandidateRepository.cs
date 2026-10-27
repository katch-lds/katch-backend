// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="ICandidateRepository"/> sobre o <see cref="KatchDbContext"/>.
/// </summary>
public sealed class CandidateRepository : ICandidateRepository
{
    private readonly KatchDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateRepository"/> class.
    /// </summary>
    /// <param name="context">Contexto da base de dados do pedido.</param>
    public CandidateRepository(KatchDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public async Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        await this.context.Candidates.AddAsync(candidate, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Candidates.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<Candidate>> ListQuotaDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // O CHECK da tabela só admite next_swipe_available_at preenchido com a quota a 0, por isso este filtro basta.
        return this.context.Candidates
            .Where(candidate => candidate.NextSwipeAvailableAt != null && candidate.NextSwipeAvailableAt <= now)
            .ToListAsync(cancellationToken);
    }
}
