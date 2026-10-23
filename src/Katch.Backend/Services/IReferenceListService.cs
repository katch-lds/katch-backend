// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Consulta e manutenção das listas pré-definidas (modelo de classes do backend v02, secção 5.1). As operações das
/// competências e dos benefícios são acrescentadas pela Issue dessas listas (I049) e pela gestão das listas (I062).
/// </summary>
public interface IReferenceListService
{
    /// <summary>
    /// Lista as localidades por ordem alfabética da designação «Localidade (Distrito)», sem coordenadas (documentação
    /// da API v02, secção 6.13.1).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As localidades.</returns>
    public Task<List<ReferenceItemDto>> ListLocationsAsync(CancellationToken cancellationToken = default);
}
