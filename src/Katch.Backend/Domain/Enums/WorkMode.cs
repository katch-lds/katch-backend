// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Regime de trabalho (parâmetro P16). Tipo PostgreSQL <c>work_mode</c>.
/// </summary>
public enum WorkMode
{
    /// <summary>
    /// Presencial.
    /// </summary>
    OnSite,

    /// <summary>
    /// Híbrido.
    /// </summary>
    Hybrid,

    /// <summary>
    /// Remoto.
    /// </summary>
    Remote,
}
