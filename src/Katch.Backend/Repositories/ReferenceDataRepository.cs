// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Linq.Expressions;
using System.Reflection;
using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="IReferenceDataRepository"/> sobre o <see cref="KatchDbContext"/>.
/// </summary>
public sealed class ReferenceDataRepository : IReferenceDataRepository
{
    // string.ToLower() sem argumentos é o método que os fornecedores do EF Core traduzem para lower() do SQL.
    private static readonly MethodInfo SqlLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

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
    public Task<List<Benefit>> ListBenefitsAsync(CancellationToken cancellationToken = default)
    {
        return this.context.Benefits.AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Skills.FirstOrDefaultAsync(skill => skill.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Benefit?> GetBenefitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Benefits.FirstOrDefaultAsync(benefit => benefit.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> SkillNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default)
    {
        return this.context.Skills
            .Where(HasName<Skill>(name))
            .AnyAsync(skill => exceptId == null || skill.Id != exceptId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> BenefitNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default)
    {
        return this.context.Benefits
            .Where(HasName<Benefit>(name))
            .AnyAsync(benefit => exceptId == null || benefit.Id != exceptId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddSkillAsync(Skill skill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        await this.context.Skills.AddAsync(skill, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddBenefitAsync(Benefit benefit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(benefit);

        await this.context.Benefits.AddAsync(benefit, cancellationToken);
    }

    /// <summary>
    /// Compõe <c>lower(name) = lower(@name)</c>, a mesma expressão dos índices <c>uq_skill_name</c> e
    /// <c>uq_benefit_name</c>.
    /// </summary>
    private static Expression<Func<TEntity, bool>> HasName<TEntity>(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        ParameterExpression entity = Expression.Parameter(typeof(TEntity), "item");
        Expression storedName = Expression.Call(Expression.Property(entity, nameof(Skill.Name)), SqlLower);
        Expression requestedName = Expression.Property(
            Expression.Constant(new NameParameter(name.ToLowerInvariant())),
            nameof(NameParameter.Value));

        return Expression.Lambda<Func<TEntity, bool>>(Expression.Equal(storedName, requestedName), entity);
    }

    private sealed record NameParameter(string Value);
}
