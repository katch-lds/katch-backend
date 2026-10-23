// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Dtos;

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Emite a credencial JWT de uma conta (documentação da API v02, secção 3.1).
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Emite a credencial de uma conta, válida durante o tempo definido em <c>JwtOptions</c> a partir do instante
    /// de emissão.
    /// </summary>
    /// <param name="user">Conta a que a credencial se destina.</param>
    /// <returns>A credencial, o instante em que expira e a identificação da conta.</returns>
    public LoginResponse CreateToken(AppUser user);
}
