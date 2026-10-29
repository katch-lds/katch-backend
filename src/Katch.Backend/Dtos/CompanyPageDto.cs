// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Página de apresentação da Empresa (documentação da API v02, secções 6.3.3, 6.4.5, 6.4.9 e 8.3; modelo de classes do
/// backend v02, secção 7.1). Não inclui os contactos da Empresa, que o Candidato só vê depois do match (RF025).
/// </summary>
/// <param name="CompanyId">Identificador da Empresa.</param>
/// <param name="CompanyName">Designação social.</param>
/// <param name="Description">Descrição da atividade, ou <see langword="null"/> se não foi gravada.</param>
/// <param name="Website">Sítio na Internet, ou <see langword="null"/> se não foi gravado.</param>
/// <param name="LogoUrl">URL da API que serve o logótipo, ou <see langword="null"/> se não foi carregado.</param>
/// <param name="PhotoUrls">URL das fotografias da galeria, pela ordem de apresentação; lista vazia se não houver.</param>
public sealed record CompanyPageDto(
    Guid CompanyId,
    string CompanyName,
    string? Description,
    string? Website,
    string? LogoUrl,
    IReadOnlyList<string> PhotoUrls);
