// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Uma vaga que cumpre o filtro do RF014 para um Candidato, com a distância à localidade do Candidato (modelo de classes
/// do backend v02, secção 4; RF013 a RF015).
/// </summary>
/// <param name="Job">Vaga, lida sem acompanhamento do contexto.</param>
/// <param name="DistanceKm">
/// Distância em linha reta entre a localidade do Candidato e a da vaga, calculada na consulta com a fórmula de Haversine
/// e arredondada às unidades (RF015).
/// </param>
public sealed record EligibleJob(Job Job, int DistanceKm);
