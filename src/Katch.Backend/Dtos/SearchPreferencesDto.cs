// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Preferências de procura do Candidato (documentação da API v02, secção 8.2).
/// </summary>
/// <param name="MaxDistanceKm">Distância máxima em quilómetros, de 1 a 500 (P06); <see langword="null"/> sem filtro.</param>
/// <param name="MinSalaryExpectation">Pretensão salarial mínima em euros; <see langword="null"/> sem filtro.</param>
/// <param name="WorkModes">Regimes de trabalho pretendidos, sem repetidos; vazio para todos.</param>
/// <param name="ContractTypes">Tipos de contrato pretendidos, sem repetidos; vazio para todos.</param>
public sealed record SearchPreferencesDto(
    int? MaxDistanceKm,
    decimal? MinSalaryExpectation,
    IReadOnlyList<WorkMode> WorkModes,
    IReadOnlyList<ContractType> ContractTypes);
