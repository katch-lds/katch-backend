// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Dados do perfil profissional que o Candidato edita (documentação da API v02, secção 6.2.2). Substitui os quatro
/// campos: <see langword="null"/> limpa os opcionais.
/// </summary>
/// <param name="DesiredRole">Função pretendida, até 150 caracteres (P14).</param>
/// <param name="LocationId">Identificador da localidade da lista pré-definida; a falta do campo é indicada como obrigatória.</param>
/// <param name="Availability">Disponibilidade para começar.</param>
/// <param name="ExperienceSummary">Resumo da experiência, até 1000 caracteres (P08).</param>
public sealed record UpdateCandidateProfileRequest(
    string? DesiredRole,
    Guid LocationId,
    Availability? Availability,
    string? ExperienceSummary)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty.
    /// </summary>
    public static UpdateCandidateProfileRequest Empty { get; } = new(null, Guid.Empty, null, null);
}
