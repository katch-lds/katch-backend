// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Perfil profissional do Candidato, devolvido pelas operações do perfil (documentação da API v02, secções 6.2 e 8.2).
/// </summary>
/// <param name="CandidateId">Identificador do Candidato.</param>
/// <param name="FullName">Nome do Candidato.</param>
/// <param name="Email">Correio eletrónico da conta.</param>
/// <param name="PhoneNumber">Contacto telefónico.</param>
/// <param name="LocationId">Identificador da localidade.</param>
/// <param name="LocationName">Designação da localidade, «Localidade (Distrito)».</param>
/// <param name="DesiredRole">Função pretendida, quando indicada.</param>
/// <param name="Availability">Disponibilidade, quando indicada.</param>
/// <param name="ExperienceSummary">Resumo da experiência, quando indicado.</param>
/// <param name="PhotoUrl">URL da API que serve a fotografia de perfil (secção 2.6), quando existe.</param>
/// <param name="HasCv">Indica se o Candidato enviou o curriculum vitae.</param>
/// <param name="Links">Hiperligações profissionais, de 0 a 3, pela ordem da posição.</param>
/// <param name="Skills">Competências da lista pré-definida.</param>
/// <param name="CustomSkills">Competências da opção «Outro».</param>
/// <param name="Preferences">Preferências de procura.</param>
public sealed record CandidateProfileDto(
    Guid CandidateId,
    string FullName,
    string Email,
    string PhoneNumber,
    Guid LocationId,
    string LocationName,
    string? DesiredRole,
    Availability? Availability,
    string? ExperienceSummary,
    string? PhotoUrl,
    bool HasCv,
    IReadOnlyList<string> Links,
    IReadOnlyList<ReferenceItemDto> Skills,
    IReadOnlyList<CustomSkillDto> CustomSkills,
    SearchPreferencesDto Preferences);
