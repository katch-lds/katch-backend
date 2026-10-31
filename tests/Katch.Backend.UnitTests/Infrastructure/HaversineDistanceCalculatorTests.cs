// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure;

namespace Katch.Backend.UnitTests.Infrastructure;

/// <summary>
/// Testes de <see cref="HaversineDistanceCalculator"/>: distância em linha reta entre duas localidades, em km arredondados
/// às unidades (RF015), a mesma da consulta do cartão de vaga.
/// </summary>
public sealed class HaversineDistanceCalculatorTests
{
    private const decimal BragaLatitude = 41.545450m;
    private const decimal BragaLongitude = -8.426507m;

    private readonly HaversineDistanceCalculator calculator = new();

    /// <summary>
    /// A distância de uma localidade a ela própria é 0 km.
    /// </summary>
    [Fact]
    public void SameLocationIsZero()
    {
        Location braga = new("Braga", "Braga", BragaLatitude, BragaLongitude);

        Assert.Equal(0, this.calculator.DistanceKm(braga, braga));
    }

    /// <summary>
    /// Ao longo do meridiano a distância de Haversine é R·Δφ: as localidades a 1, 22 e 300 km a norte de Braga ficam a
    /// essa distância, nos dois sentidos.
    /// </summary>
    /// <param name="kilometres">Distância a norte de Braga, em km.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(22)]
    [InlineData(300)]
    public void DistanceAlongTheMeridianIsTheArcLength(int kilometres)
    {
        Location braga = new("Braga", "Braga", BragaLatitude, BragaLongitude);
        Location north = NorthOfBraga(kilometres);

        Assert.Equal(kilometres, this.calculator.DistanceKm(braga, north));
        Assert.Equal(kilometres, this.calculator.DistanceKm(north, braga));
    }

    /// <summary>
    /// A distância é arredondada às unidades com as metades para cima, como no cartão: 12,4 km dá 12 e 12,6 km dá 13.
    /// </summary>
    /// <param name="kilometres">Distância exata, em km.</param>
    /// <param name="expected">Distância arredondada.</param>
    [Theory]
    [InlineData(12.4, 12)]
    [InlineData(12.6, 13)]
    public void DistanceIsRoundedToTheUnit(double kilometres, int expected)
    {
        Location braga = new("Braga", "Braga", BragaLatitude, BragaLongitude);

        Assert.Equal(expected, this.calculator.DistanceKm(braga, NorthOfBraga(kilometres)));
    }

    /// <summary>
    /// Para localidades antípodas a distância é metade do perímetro (π·R), sem erro de domínio no arco seno.
    /// </summary>
    [Fact]
    public void AntipodesAreHalfTheCircumference()
    {
        Location origin = new("Origem", "Teste", 41.5m, -8.5m);
        Location antipode = new("Antípoda", "Teste", -41.5m, 171.5m);

        int expected = (int)Math.Round(Math.PI * HaversineDistanceCalculator.EarthRadiusKm, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, this.calculator.DistanceKm(origin, antipode));
    }

    /// <summary>
    /// As localidades são obrigatórias.
    /// </summary>
    [Fact]
    public void NullLocationsAreRejected()
    {
        Location braga = new("Braga", "Braga", BragaLatitude, BragaLongitude);

        Assert.Throws<ArgumentNullException>(() => this.calculator.DistanceKm(null!, braga));
        Assert.Throws<ArgumentNullException>(() => this.calculator.DistanceKm(braga, null!));
    }

    private static Location NorthOfBraga(double kilometres)
    {
        decimal latitude = Math.Round(
            BragaLatitude + (decimal)(kilometres / (HaversineDistanceCalculator.EarthRadiusKm * Math.PI / 180)),
            6);
        return new Location("Norte", "Braga", latitude, BragaLongitude);
    }
}
