// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.AspNetCore.Identity;

namespace Katch.Backend.Services;

/// <summary>
/// <see cref="IPasswordHasher"/> com o <see cref="PasswordHasher{TUser}"/> do ASP.NET Core Identity (PBKDF2 com valor
/// aleatório por conta), o mesmo usado na conta de Administrador dos dados iniciais (modelo de classes do backend v02,
/// secção 5.2; RNF003).
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    // O PasswordHasher não usa a conta no cálculo; por isso, a mesma instância serve para todas.
    private static readonly AppUser NoUser = null!;

    private readonly PasswordHasher<AppUser> hasher = new();

    /// <inheritdoc/>
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        return this.hasher.HashPassword(NoUser, password);
    }

    /// <inheritdoc/>
    public bool Verify(string hash, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(hash);
        ArgumentNullException.ThrowIfNull(password);

        return this.hasher.VerifyHashedPassword(NoUser, hash, password) != PasswordVerificationResult.Failed;
    }
}
