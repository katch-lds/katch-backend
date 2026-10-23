// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Security.Claims;
using Katch.Backend.Infrastructure.Authentication;

namespace Katch.Backend.UnitTests.Infrastructure;

/// <summary>
/// Testes de <see cref="ClaimsPrincipalExtensions"/> (modelo de classes do backend v02, DC-05).
/// </summary>
public sealed class ClaimsPrincipalExtensionsTests
{
    /// <summary>
    /// A claim <c>sub</c> com um identificador válido dá a conta autenticada.
    /// </summary>
    [Fact]
    public void ValidSubjectGivesTheAccountId()
    {
        var id = Guid.NewGuid();
        ClaimsPrincipal principal = Principal(new Claim("sub", id.ToString()));

        bool found = principal.TryGetAccountId(out Guid accountId);

        Assert.True(found);
        Assert.Equal(id, accountId);
    }

    /// <summary>
    /// Sem a claim <c>sub</c>, ou com um valor que não é um identificador, não há conta autenticada.
    /// </summary>
    /// <param name="subject">Valor da claim, ou <see langword="null"/> para a omitir.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nao-e-um-guid")]
    public void MissingOrInvalidSubjectGivesNoAccount(string? subject)
    {
        ClaimsPrincipal principal = subject is null ? Principal() : Principal(new Claim("sub", subject));

        bool found = principal.TryGetAccountId(out Guid accountId);

        Assert.False(found);
        Assert.Equal(Guid.Empty, accountId);
    }

    /// <summary>
    /// Só a claim <c>sub</c> identifica a conta: outras claims com um identificador não servem.
    /// </summary>
    [Fact]
    public void OnlyTheSubjectClaimIdentifiesTheAccount()
    {
        ClaimsPrincipal principal = Principal(new Claim("userId", Guid.NewGuid().ToString()), new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        Assert.False(principal.TryGetAccountId(out _));
    }

    /// <summary>
    /// Sem utilizador, o pedido é recusado.
    /// </summary>
    [Fact]
    public void NullPrincipalIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ((ClaimsPrincipal)null!).TryGetAccountId(out _));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }
}
