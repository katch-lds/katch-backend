// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Cálculo da distância em linha reta entre duas localidades (modelo de classes do backend v02, secção 5; RF015).
/// </summary>
public interface IDistanceCalculator
{
    /// <summary>
    /// Calcula a distância em linha reta entre duas localidades, a partir das coordenadas.
    /// </summary>
    /// <param name="origin">Localidade de origem.</param>
    /// <param name="destination">Localidade de destino.</param>
    /// <returns>A distância em km, arredondada às unidades.</returns>
    public int DistanceKm(Location origin, Location destination);
}
