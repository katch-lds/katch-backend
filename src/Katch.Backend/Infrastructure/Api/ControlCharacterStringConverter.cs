// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.Json;
using System.Text.Json.Serialization;
using Katch.Backend.Infrastructure.Errors;

namespace Katch.Backend.Infrastructure.Api;

/// <summary>
/// Lê os textos dos pedidos JSON e recusa os que tenham caracteres de controlo (categoria Unicode <c>Cc</c>), exceto
/// a tabulação e as mudanças de linha (SEG-006). A recusa chega ao cliente como ERR-10 no campo, com a razão
/// <see cref="FieldReasons.ControlCharacters"/>. A escrita não muda.
/// </summary>
internal sealed class ControlCharacterStringConverter : JsonConverter<string>
{
    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        if (value is not null && value.Any(IsForbidden))
        {
            throw new JsonException(FieldReasons.ControlCharacters);
        }

        return value;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value);
    }

    private static bool IsForbidden(char character)
    {
        return char.IsControl(character) && character is not ('\t' or '\n' or '\r');
    }
}
