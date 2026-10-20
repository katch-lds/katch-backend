// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="Location"/> (modelo de dados v02, secção 6.5).
/// </summary>
public sealed class LocationTests
{
    /// <summary>
    /// Uma localidade válida guarda os dados indicados e recebe um identificador.
    /// </summary>
    [Fact]
    public void ValidLocationKeepsTheData()
    {
        var location = new Location("Leiria", "Leiria", 39.743620m, -8.807050m);

        Assert.NotEqual(Guid.Empty, location.Id);
        Assert.Equal("Leiria", location.Name);
        Assert.Equal("Leiria", location.District);
        Assert.Equal(39.743620m, location.Latitude);
        Assert.Equal(-8.807050m, location.Longitude);
    }

    /// <summary>
    /// Os limites da latitude e da longitude são aceites.
    /// </summary>
    [Fact]
    public void CoordinateLimitsAreAccepted()
    {
        _ = new Location("Polo Norte", "Ártico", 90m, 180m);
        _ = new Location("Polo Sul", "Antártida", -90m, -180m);
    }

    /// <summary>
    /// Coordenadas fora do intervalo são rejeitadas.
    /// </summary>
    /// <param name="latitude">Latitude.</param>
    /// <param name="longitude">Longitude.</param>
    [Theory]
    [InlineData(90.000001, 0)]
    [InlineData(-90.000001, 0)]
    [InlineData(0, 180.000001)]
    [InlineData(0, -180.000001)]
    public void CoordinatesOutOfRangeAreRejected(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Location("X", "Y", (decimal)latitude, (decimal)longitude));
    }

    /// <summary>
    /// Nome ou distrito vazios ou demasiado longos são rejeitados.
    /// </summary>
    [Fact]
    public void InvalidNameOrDistrictIsRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Location(" ", "Leiria", 0m, 0m));
        Assert.ThrowsAny<ArgumentException>(() => new Location("Leiria", string.Empty, 0m, 0m));
        Assert.ThrowsAny<ArgumentException>(() => new Location(new string('a', 101), "Leiria", 0m, 0m));
        Assert.ThrowsAny<ArgumentException>(() => new Location("Leiria", new string('a', 51), 0m, 0m));
    }
}
