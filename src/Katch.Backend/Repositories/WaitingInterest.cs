// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Um interesse em espera numa vaga, com os dados do Candidato que a lista de candidatos em espera apresenta (RF060).
/// </summary>
/// <param name="MatchId">Identificador do interesse (linha de <c>match</c>).</param>
/// <param name="CandidateId">Identificador do Candidato.</param>
/// <param name="FullName">Nome do Candidato.</param>
/// <param name="DesiredRole">Função pretendida, quando indicada.</param>
/// <param name="Location">Localidade do Candidato, lida sem acompanhamento do contexto.</param>
/// <param name="InterestAt">Data e hora da manifestação de interesse (<c>candidate_action_at</c>).</param>
public sealed record WaitingInterest(
    Guid MatchId,
    Guid CandidateId,
    string FullName,
    string? DesiredRole,
    Location Location,
    DateTimeOffset InterestAt);
