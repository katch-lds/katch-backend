// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Competência do Candidato no perfil completo apresentado ao Recrutador (documentação da API v02, secção 8.4).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Name">Designação da competência.</param>
/// <param name="MatchesJob">
/// Indica se a competência é uma das pretendidas na vaga (RF062); só pode ser <see langword="true"/> nas competências
/// da lista pré-definida.
/// </param>
public sealed record SkillTagModel(string Name, bool MatchesJob);
