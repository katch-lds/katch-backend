// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Cors;

/// <summary>
/// Configuração do CORS, lida da secção <c>Cors</c>. Só as origens são configuráveis (variável de ambiente
/// <c>Cors__AllowedOrigins</c>); os métodos e os cabeçalhos são os do contrato da API v02 e ficam no código. Sem
/// origens configuradas, nenhum pedido de outra origem é autorizado.
/// </summary>
public sealed class KatchCorsOptions
{
    /// <summary>
    /// Nome da secção de configuração.
    /// </summary>
    public const string SectionName = "Cors";

    /// <summary>
    /// Gets or sets the allowed origins, separated by commas (for example
    /// <c>http://localhost:5173,http://localhost:4173</c>). Each one is a scheme, a host and an optional port, with no path.
    /// </summary>
    public string? AllowedOrigins { get; set; }

    /// <summary>
    /// Separa as origens configuradas, sem espaços nem entradas vazias.
    /// </summary>
    /// <returns>As origens pela ordem em que foram configuradas.</returns>
    public IReadOnlyList<string> ParseOrigins() =>
        (this.AllowedOrigins ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
