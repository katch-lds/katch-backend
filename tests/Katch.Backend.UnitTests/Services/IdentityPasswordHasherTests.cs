// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Services;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes de <see cref="IdentityPasswordHasher"/> (modelo de classes do backend v02, secção 5.2; RNF003).
/// </summary>
public sealed class IdentityPasswordHasherTests
{
    private readonly IdentityPasswordHasher hasher = new();

    /// <summary>
    /// O resumo não contém a palavra-passe, cabe nos 255 caracteres da coluna e é verificável.
    /// </summary>
    [Fact]
    public void HashIsIrreversibleFitsTheColumnAndVerifies()
    {
        string hash = this.hasher.Hash("katch2026");

        Assert.DoesNotContain("katch2026", hash, StringComparison.Ordinal);
        Assert.InRange(hash.Length, 1, AppUser.MaxLength);
        Assert.True(this.hasher.Verify(hash, "katch2026"));
        Assert.False(this.hasher.Verify(hash, "Katch2026"));
    }

    /// <summary>
    /// A mesma palavra-passe dá resumos diferentes, por causa do valor aleatório de cada cálculo.
    /// </summary>
    [Fact]
    public void SamePasswordProducesDifferentHashes()
    {
        Assert.NotEqual(this.hasher.Hash("katch2026"), this.hasher.Hash("katch2026"));
    }
}
