// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Competência do Candidato no perfil completo apresentado ao Recrutador (documentação da API v02, secção 8.4).
/// </summary>
/// <param name="Name">Designação da competência.</param>
/// <param name="MatchesJob">
/// Indica se a competência é uma das pretendidas na vaga (RF062); só pode ser <see langword="true"/> nas competências
/// da lista pré-definida.
/// </param>
public sealed record SkillTagDto(string Name, bool MatchesJob);
