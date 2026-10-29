// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Converte os caminhos guardados nos URLs da API que servem os ficheiros (documentação da API v02, secção 2.6): o
/// caminho no sistema de ficheiros nunca vai ao cliente.
/// </summary>
public interface IFileUrlBuilder
{
    /// <summary>
    /// Converte <c>&lt;tipo&gt;/&lt;nome&gt;</c> em <c>/api/files/&lt;TIPO&gt;/&lt;nome&gt;</c>.
    /// </summary>
    /// <param name="path">Caminho relativo guardado, ou <see langword="null"/>.</param>
    /// <returns>O URL, ou <see langword="null"/> se não houver caminho.</returns>
    public string? ToUrl(string? path);

    /// <summary>
    /// Converte vários caminhos, pela mesma ordem.
    /// </summary>
    /// <param name="paths">Caminhos relativos guardados.</param>
    /// <returns>Os URLs.</returns>
    public List<string> ToUrls(List<string> paths);
}
