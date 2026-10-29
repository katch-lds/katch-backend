// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="IReferenceDataRepository"/> sobre o <see cref="KatchDbContext"/>.
/// </summary>
public sealed class ReferenceDataRepository : IReferenceDataRepository
{
    private readonly KatchDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceDataRepository"/> class.
    /// </summary>
    /// <param name="context">Contexto da base de dados do pedido.</param>
    public ReferenceDataRepository(KatchDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
    {
        return this.context.Locations.AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Locations.AnyAsync(location => location.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Location?> GetLocationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Locations.AsNoTracking().FirstOrDefaultAsync(location => location.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<Skill>> ListSkillsAsync(CancellationToken cancellationToken = default)
    {
        return this.context.Skills.AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Skills.FirstOrDefaultAsync(skill => skill.Id == id, cancellationToken);
    }
}
