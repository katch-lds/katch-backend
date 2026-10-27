// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// Motivo pelo qual a resposta do Candidato a uma vaga foi rejeitada.
/// </summary>
public enum JobAnswerRejection
{
    /// <summary>
    /// A vaga não existe, está eliminada ou nunca foi publicada (<c>404</c> ERR-20).
    /// </summary>
    NotFound,

    /// <summary>
    /// O Candidato já respondeu a esta vaga (<c>409</c> ERR-32).
    /// </summary>
    AlreadyAnswered,

    /// <summary>
    /// A vaga já não cumpre o RF014 (<c>409</c> ERR-33).
    /// </summary>
    Unavailable,
}
