// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Tipo de conta de utilizador. Tipo PostgreSQL <c>user_type</c>.
/// </summary>
public enum UserType
{
    /// <summary>
    /// Candidato; acede pela aplicação móvel.
    /// </summary>
    Candidate,

    /// <summary>
    /// Recrutador; acede pela área de gestão web.
    /// </summary>
    Recruiter,

    /// <summary>
    /// Administrador da plataforma.
    /// </summary>
    Admin,
}
