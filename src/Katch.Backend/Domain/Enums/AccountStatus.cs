// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Estado de uma conta de utilizador. Tipo PostgreSQL <c>account_status</c>.
/// </summary>
public enum AccountStatus
{
    /// <summary>
    /// Conta ativa.
    /// </summary>
    Active,

    /// <summary>
    /// Conta bloqueada pelo Administrador.
    /// </summary>
    Blocked,

    /// <summary>
    /// Conta de Candidato suspensa pelo Administrador.
    /// </summary>
    Suspended,
}
