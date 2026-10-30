// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Perfil completo do Candidato apresentado ao Recrutador para uma vaga em que o Candidato está em espera
/// (documentação da API v02, secções 6.6.2 e 8.4). Não inclui contactos, pretensão salarial nem preferências de procura
/// (modelo de dados v02, secção 8).
/// </summary>
/// <param name="MatchId">Identificador do interesse (linha de <c>match</c>).</param>
/// <param name="FullName">Nome do Candidato.</param>
/// <param name="PhotoUrl">URL da API que serve a fotografia de perfil (secção 2.6), quando existe.</param>
/// <param name="DesiredRole">Função pretendida, quando indicada.</param>
/// <param name="LocationName">Localidade do Candidato, «Localidade (Distrito)».</param>
/// <param name="Availability">Disponibilidade, quando indicada.</param>
/// <param name="ExperienceSummary">Resumo da experiência, quando indicado.</param>
/// <param name="Links">Hiperligações profissionais, de 0 a 3, pela ordem da posição.</param>
/// <param name="Skills">Competências da lista e da opção «Outro», com as coincidentes com a vaga identificadas.</param>
/// <param name="HasCv">Indica se o Candidato enviou o curriculum vitae, obtido em 6.6.3.</param>
/// <param name="ProfileOpenedAt">Data e hora da primeira abertura do perfil para este interesse (RF118).</param>
public sealed record CandidateFullProfileDto(
    Guid MatchId,
    string FullName,
    string? PhotoUrl,
    string? DesiredRole,
    string LocationName,
    Availability? Availability,
    string? ExperienceSummary,
    IReadOnlyList<string> Links,
    IReadOnlyList<SkillTagDto> Skills,
    bool HasCv,
    DateTimeOffset ProfileOpenedAt);
