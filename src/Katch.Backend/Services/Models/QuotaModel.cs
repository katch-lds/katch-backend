// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Quota de interesses do Candidato (documentação da API v02, secção 8.2).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Available">Interesses ainda disponíveis, de 0 a 10.</param>
/// <param name="Total">Total da quota (P11), 10.</param>
/// <param name="BlockedUntil">Fim do período de bloqueio; <see langword="null"/> fora do bloqueio.</param>
public sealed record QuotaModel(int Available, int Total, DateTimeOffset? BlockedUntil);
