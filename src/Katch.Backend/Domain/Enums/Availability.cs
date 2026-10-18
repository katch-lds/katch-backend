// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Disponibilidade do Candidato (parâmetro P14). Tipo PostgreSQL <c>availability</c>.
/// </summary>
public enum Availability
{
    /// <summary>
    /// Imediata.
    /// </summary>
    Immediate,

    /// <summary>
    /// 15 dias.
    /// </summary>
    FifteenDays,

    /// <summary>
    /// 1 mês.
    /// </summary>
    OneMonth,

    /// <summary>
    /// A combinar.
    /// </summary>
    ToBeAgreed,
}
