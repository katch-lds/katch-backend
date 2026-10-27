// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

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
}
