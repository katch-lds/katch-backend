// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Quota de interesses do Candidato (documentação da API v02, secção 8.2).
/// </summary>
/// <param name="Available">Interesses ainda disponíveis, de 0 a 10.</param>
/// <param name="Total">Total da quota (P11), 10.</param>
/// <param name="BlockedUntil">Fim do período de bloqueio; <see langword="null"/> fora do bloqueio.</param>
public sealed record QuotaDto(int Available, int Total, DateTimeOffset? BlockedUntil);
