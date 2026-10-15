// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Tipo de contrato (parâmetro P09). Tipo PostgreSQL <c>contract_type</c>.
/// </summary>
public enum ContractType
{
    /// <summary>
    /// Sem termo.
    /// </summary>
    Permanent,

    /// <summary>
    /// A termo.
    /// </summary>
    FixedTerm,

    /// <summary>
    /// Estágio.
    /// </summary>
    Internship,

    /// <summary>
    /// Prestação de serviços.
    /// </summary>
    ServiceProvision,
}
