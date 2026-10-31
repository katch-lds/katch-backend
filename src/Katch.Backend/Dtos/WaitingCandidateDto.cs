// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Candidato em espera de resposta numa vaga, na lista do Recrutador (documentação da API v02, secções 6.6.1 e 8.4). A
/// lista não tem ações de decisão (RF060).
/// </summary>
/// <param name="MatchId">Identificador do interesse (linha de <c>match</c>), usado para abrir o perfil completo.</param>
/// <param name="CandidateId">Identificador do Candidato.</param>
/// <param name="FullName">Nome do Candidato.</param>
/// <param name="DesiredRole">Função pretendida do Candidato; <see langword="null"/> se não a indicou.</param>
/// <param name="LocationName">Localidade do Candidato, «Localidade (Distrito)».</param>
/// <param name="InterestAt">Data e hora da manifestação de interesse (<c>candidate_action_at</c>).</param>
public sealed record WaitingCandidateDto(
    Guid MatchId,
    Guid CandidateId,
    string FullName,
    string? DesiredRole,
    string LocationName,
    DateTimeOffset InterestAt);
