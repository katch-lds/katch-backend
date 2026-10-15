// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Estado de uma vaga. Tipo PostgreSQL <c>job_status</c>.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Não publicada.
    /// </summary>
    Unpublished,

    /// <summary>
    /// Publicada.
    /// </summary>
    Published,

    /// <summary>
    /// Suspensa.
    /// </summary>
    Suspended,

    /// <summary>
    /// Encerrada, manualmente ou por data-limite.
    /// </summary>
    Closed,
}
