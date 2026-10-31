// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Corpo do pedido de gravação das preferências de procura (documentação da API v02, secção 6.2.3), com a forma de
/// <see cref="SearchPreferencesDto"/>. As listas são anuláveis para que a sua falta signifique sem filtro e não um erro
/// de leitura do pedido.
/// </summary>
/// <param name="MaxDistanceKm">Distância máxima em quilómetros, de 1 a 500 (P06); <see langword="null"/> sem filtro.</param>
/// <param name="MinSalaryExpectation">Pretensão salarial mínima em euros; <see langword="null"/> sem filtro.</param>
/// <param name="WorkModes">Regimes de trabalho pretendidos; <see langword="null"/> ou vazio para todos.</param>
/// <param name="ContractTypes">Tipos de contrato pretendidos; <see langword="null"/> ou vazio para todos.</param>
public sealed record UpdatePreferencesRequest(
    int? MaxDistanceKm,
    decimal? MinSalaryExpectation,
    IReadOnlyList<WorkMode>? WorkModes,
    IReadOnlyList<ContractType>? ContractTypes)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty.
    /// </summary>
    public static UpdatePreferencesRequest Empty { get; } = new(null, null, null, null);

    /// <summary>
    /// Converte o pedido nas preferências a gravar, com as listas em falta vazias.
    /// </summary>
    /// <returns>As preferências.</returns>
    public SearchPreferencesDto ToPreferences()
    {
        return new SearchPreferencesDto(this.MaxDistanceKm, this.MinSalaryExpectation, this.WorkModes ?? [], this.ContractTypes ?? []);
    }
}
