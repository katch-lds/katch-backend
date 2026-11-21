// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.Json;
using Katch.Backend.Infrastructure.Api;
using Katch.Backend.Infrastructure.Errors;

namespace Katch.Backend.UnitTests.Infrastructure;

/// <summary>
/// Testes da leitura dos textos dos pedidos JSON sem caracteres de controlo (SEG-006).
/// </summary>
public sealed class ControlCharacterStringConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new ControlCharacterStringConverter() } };

    /// <summary>
    /// Texto normal, acentos, tabulação e mudanças de linha passam sem alteração; o nulo continua nulo.
    /// </summary>
    /// <param name="json">Valor JSON.</param>
    /// <param name="expected">Texto esperado.</param>
    [Theory]
    [InlineData("\"Gestão de equipas\"", "Gestão de equipas")]
    [InlineData("\"linha 1\\r\\nlinha 2\\tfim\"", "linha 1\r\nlinha 2\tfim")]
    [InlineData("null", null)]
    public void AllowedTextIsReadUnchanged(string json, string? expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<string>(json, Options));
    }

    /// <summary>
    /// Os caracteres de controlo C0 e C1 (exceto tabulação e mudanças de linha) são recusados com a razão do campo.
    /// </summary>
    /// <param name="json">Valor JSON.</param>
    [Theory]
    [InlineData("\"a\\u0000b\"")]
    [InlineData("\"a\\u0007b\"")]
    [InlineData("\"a\\u001Bb\"")]
    [InlineData("\"a\\u007Fb\"")]
    [InlineData("\"a\\u0085b\"")]
    public void ControlCharactersAreRejected(string json)
    {
        JsonException exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<string>(json, Options));

        Assert.Equal(FieldReasons.ControlCharacters, exception.Message);
    }

    /// <summary>
    /// A escrita não muda.
    /// </summary>
    [Fact]
    public void WritingIsUnchanged()
    {
        Assert.Equal("\"a\\u0007b\"", JsonSerializer.Serialize("a\u0007b", Options));
    }
}
