// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Services.Models;

/// <summary>
/// Dados do perfil profissional que o Candidato edita (documentação da API v02, secção 6.2.2). Substitui os quatro
/// campos: <see langword="null"/> limpa os opcionais.
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="DesiredRole">Função pretendida, até 150 caracteres (P14).</param>
/// <param name="LocationId">Identificador da localidade da lista pré-definida; a falta do campo é indicada como obrigatória.</param>
/// <param name="Availability">Disponibilidade para começar.</param>
/// <param name="ExperienceSummary">Resumo da experiência, até 1000 caracteres (P08).</param>
public sealed record UpdateCandidateProfileInput(
    string? DesiredRole,
    Guid LocationId,
    Availability? Availability,
    string? ExperienceSummary)
{
    /// <summary>
    /// Gets an input without any field, equivalent to an empty request body.
    /// </summary>
    public static UpdateCandidateProfileInput Empty { get; } = new(null, Guid.Empty, null, null);
}
