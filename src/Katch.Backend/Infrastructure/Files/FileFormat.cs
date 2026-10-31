// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Formato real de um ficheiro, reconhecido pelos primeiros bytes do conteúdo e nunca pelo nome (RNF016).
/// </summary>
public enum FileFormat
{
    /// <summary>
    /// Conteúdo que não corresponde a nenhum formato aceite.
    /// </summary>
    Unknown,

    /// <summary>
    /// Imagem JPEG.
    /// </summary>
    Jpeg,

    /// <summary>
    /// Imagem PNG.
    /// </summary>
    Png,

    /// <summary>
    /// Documento PDF.
    /// </summary>
    Pdf,
}
