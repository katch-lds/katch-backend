// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.Json;
using Katch.Backend.Infrastructure.Api;

namespace Katch.Backend.UnitTests.Infrastructure;

/// <summary>
/// Testes de <see cref="UtcDateTimeOffsetJsonConverter"/> (documentação da API v02, secção 2.3).
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new UtcDateTimeOffsetJsonConverter() } };

    /// <summary>
    /// Um instante é escrito em UTC com o sufixo Z, sem frações quando não as tem, e com elas quando as tem.
    /// </summary>
    /// <param name="hours">Desvio do instante original.</param>
    /// <param name="milliseconds">Milissegundos do instante.</param>
    /// <param name="expected">Texto JSON esperado.</param>
    [Theory]
    [InlineData(0, 0, "\"2026-10-09T22:30:00Z\"")]
    [InlineData(1, 0, "\"2026-10-09T22:30:00Z\"")]
    [InlineData(0, 250, "\"2026-10-09T22:30:00.25Z\"")]
    public void WritesUtcWithZSuffix(int hours, int milliseconds, string expected)
    {
        var utc = new DateTimeOffset(2026, 10, 9, 22, 30, 0, milliseconds, TimeSpan.Zero);
        DateTimeOffset value = utc.ToOffset(TimeSpan.FromHours(hours));

        Assert.Equal(expected, JsonSerializer.Serialize(value, Options));
    }

    /// <summary>
    /// Um instante lido com qualquer desvio fica em UTC, sem perder o instante.
    /// </summary>
    [Fact]
    public void ReadsAnyOffsetAsUtc()
    {
        DateTimeOffset value = JsonSerializer.Deserialize<DateTimeOffset>("\"2026-10-09T23:30:00+01:00\"", Options);

        Assert.Equal(TimeSpan.Zero, value.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 22, 30, 0, TimeSpan.Zero), value);
    }
}
