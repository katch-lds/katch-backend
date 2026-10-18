// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Estado da resposta do Candidato a uma vaga e do match. Tipo PostgreSQL <c>match_status</c>.
/// </summary>
public enum MatchStatus
{
    /// <summary>
    /// Vaga recusada pelo Candidato.
    /// </summary>
    DeclinedByCandidate,

    /// <summary>
    /// Interesse do Candidato em espera da decisão do Recrutador.
    /// </summary>
    Waiting,

    /// <summary>
    /// Match confirmado.
    /// </summary>
    Matched,

    /// <summary>
    /// Candidato recusado pelo Recrutador.
    /// </summary>
    RejectedByRecruiter,
}
