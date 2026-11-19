// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Competência da opção «Outro» do perfil do Candidato (documentação da API v02, secção 8.2).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="CustomSkillId">Identificador (<c>candidate_custom_skill.id</c>), usado para a retirar (6.2.11).</param>
/// <param name="Label">Designação indicada pelo Candidato (P07).</param>
public sealed record CustomSkillModel(Guid CustomSkillId, string Label);
