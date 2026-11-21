// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.RateLimiting;

/// <summary>
/// Nomes das políticas de limite de pedidos por IP dos endpoints anónimos (<c>[EnableRateLimiting]</c>). Os pedidos
/// autenticados ficam no limite global por conta.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Início de sessão.
    /// </summary>
    public const string Login = "Login";

    /// <summary>
    /// Registo de Candidato e criação de conta de Recrutador, com uma contagem comum.
    /// </summary>
    public const string Registration = "Registration";

    /// <summary>
    /// Lista anónima das localidades.
    /// </summary>
    public const string Locations = "Locations";
}
