// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Estado do registo de uma Empresa. Tipo PostgreSQL <c>company_status</c>.
/// </summary>
public enum CompanyStatus
{
    /// <summary>
    /// Pendente de aprovação.
    /// </summary>
    Pending,

    /// <summary>
    /// Aprovada.
    /// </summary>
    Approved,

    /// <summary>
    /// Recusada, com motivo.
    /// </summary>
    Rejected,

    /// <summary>
    /// Suspensa pelo Administrador.
    /// </summary>
    Suspended,
}
