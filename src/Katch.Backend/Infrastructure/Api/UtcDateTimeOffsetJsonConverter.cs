// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Katch.Backend.Infrastructure.Api;

/// <summary>
/// Datas e horas em ISO 8601, em UTC e com o sufixo <c>Z</c> (documentação da API v02, secção 2.3):
/// <c>2026-10-09T14:30:00Z</c>. As frações de segundo só aparecem quando existem, para que o parâmetro <c>after</c> das
/// conversas possa repetir o instante exato de uma mensagem.
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    /// <inheritdoc/>
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDateTimeOffset().ToUniversalTime();
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
    }
}
