// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Implementação de <see cref="IDistanceCalculator"/> com a fórmula de Haversine (RF015). Usa o mesmo raio da Terra e o
/// mesmo arredondamento da consulta das vagas elegíveis do <c>JobRepository</c>, por isso o detalhe de uma vaga mostra a
/// mesma distância do seu cartão.
/// </summary>
public sealed class HaversineDistanceCalculator : IDistanceCalculator
{
    /// <summary>
    /// Raio médio da Terra em km.
    /// </summary>
    public const double EarthRadiusKm = 6371.0088;

    /// <inheritdoc/>
    public int DistanceKm(Location origin, Location destination)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        double originLatitude = ToRadians((double)origin.Latitude);
        double destinationLatitude = ToRadians((double)destination.Latitude);
        double halfLatitude = Math.Sin((destinationLatitude - originLatitude) / 2);
        double halfLongitude = Math.Sin((ToRadians((double)destination.Longitude) - ToRadians((double)origin.Longitude)) / 2);

        // d = 2R·asin(√a), com a = sin²(Δφ/2) + cos φ1 · cos φ2 · sin²(Δλ/2). O Math.Min evita um asin fora do domínio por
        // arredondamento quando as localidades são quase antípodas.
        double a = (halfLatitude * halfLatitude)
            + (Math.Cos(originLatitude) * Math.Cos(destinationLatitude) * halfLongitude * halfLongitude);
        double distance = 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        return (int)Math.Round(distance, MidpointRounding.AwayFromZero);
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
