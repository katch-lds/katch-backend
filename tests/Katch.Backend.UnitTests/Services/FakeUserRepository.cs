// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Repositories;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Repositório de contas simulado, só com a consulta por identificador que os serviços de avaliação usam.
/// </summary>
internal sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, AppUser> byId = [];

    /// <summary>
    /// Acrescenta uma conta ao repositório.
    /// </summary>
    /// <param name="user">Conta a guardar.</param>
    public void Add(AppUser user)
    {
        this.byId[user.Id] = user;
    }

    /// <inheritdoc/>
    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.byId.GetValueOrDefault(id));
    }

    /// <inheritdoc/>
    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    Task IUserRepository.AddAsync(AppUser user, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public Task AddRecruiterAsync(Recruiter recruiter, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
