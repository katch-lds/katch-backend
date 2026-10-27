// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Leitura da conta autenticada a partir da credencial de sessão. O identificador do utilizador vem sempre da
/// credencial, nunca do corpo nem da rota do pedido (modelo de classes do backend v02, DC-05; RNF006).
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Lê o identificador da conta (claim <c>sub</c>) da credencial de sessão.
    /// </summary>
    /// <param name="principal">Utilizador autenticado do pedido.</param>
    /// <param name="accountId">O identificador da conta, se a claim existir e for um <see cref="Guid"/>.</param>
    /// <returns><see langword="true"/> se a credencial identificar uma conta.</returns>
    public static bool TryGetAccountId(this ClaimsPrincipal principal, out Guid accountId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId);
    }
}
