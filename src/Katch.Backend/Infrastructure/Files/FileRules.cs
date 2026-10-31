// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.Json;

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Regras dos ficheiros por tipo (documentação da API v02, secção 2.6) e conversões entre formato, extensão, tipo de
/// conteúdo, pasta de armazenamento e nome na rota.
/// </summary>
public static class FileRules
{
    /// <summary>
    /// Número de bytes de um megabyte nos limites de dimensão.
    /// </summary>
    public const int Megabyte = 1024 * 1024;

    /// <summary>
    /// Número de bytes iniciais necessários para reconhecer qualquer formato aceite.
    /// </summary>
    public const int SignatureLength = 8;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly byte[] PdfSignature = [0x25, 0x50, 0x44, 0x46, 0x2D];

    /// <summary>
    /// Dimensão máxima, em bytes, de um ficheiro do tipo indicado.
    /// </summary>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <returns>O máximo: 2 MB para o logótipo e 5 MB para os restantes.</returns>
    public static int MaxBytes(FileKind kind)
    {
        return kind == FileKind.CompanyLogo ? 2 * Megabyte : 5 * Megabyte;
    }

    /// <summary>
    /// Indica se o formato é aceite para o tipo de ficheiro (o PDF só para o curriculum vitae).
    /// </summary>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <param name="format">Formato do conteúdo.</param>
    /// <returns><see langword="true"/> se o formato for aceite.</returns>
    public static bool IsAccepted(FileKind kind, FileFormat format)
    {
        return kind == FileKind.CurriculumVitae
            ? format == FileFormat.Pdf
            : format is FileFormat.Jpeg or FileFormat.Png;
    }

    /// <summary>
    /// Reconhece o formato pelos primeiros bytes do conteúdo.
    /// </summary>
    /// <param name="header">Primeiros bytes do ficheiro.</param>
    /// <returns>O formato, ou <see cref="FileFormat.Unknown"/>.</returns>
    public static FileFormat Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegSignature))
        {
            return FileFormat.Jpeg;
        }

        if (header.StartsWith(PngSignature))
        {
            return FileFormat.Png;
        }

        return header.StartsWith(PdfSignature) ? FileFormat.Pdf : FileFormat.Unknown;
    }

    /// <summary>
    /// Formato que a extensão do nome indica.
    /// </summary>
    /// <param name="extension">Extensão com o ponto, sem distinção de maiúsculas.</param>
    /// <returns>O formato, ou <see cref="FileFormat.Unknown"/>.</returns>
    public static FileFormat FromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => FileFormat.Jpeg,
            ".png" => FileFormat.Png,
            ".pdf" => FileFormat.Pdf,
            _ => FileFormat.Unknown,
        };
    }

    /// <summary>
    /// Extensão com que o servidor guarda o formato.
    /// </summary>
    /// <param name="format">Formato do conteúdo.</param>
    /// <returns><c>.jpg</c>, <c>.png</c> ou <c>.pdf</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Se o formato não for conhecido.</exception>
    public static string Extension(FileFormat format)
    {
        return format switch
        {
            FileFormat.Jpeg => ".jpg",
            FileFormat.Png => ".png",
            FileFormat.Pdf => ".pdf",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Formato desconhecido."),
        };
    }

    /// <summary>
    /// Tipo de conteúdo (<c>Content-Type</c>) do formato.
    /// </summary>
    /// <param name="format">Formato do conteúdo.</param>
    /// <returns><c>image/jpeg</c>, <c>image/png</c> ou <c>application/pdf</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Se o formato não for conhecido.</exception>
    public static string ContentType(FileFormat format)
    {
        return format switch
        {
            FileFormat.Jpeg => "image/jpeg",
            FileFormat.Png => "image/png",
            FileFormat.Pdf => "application/pdf",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Formato desconhecido."),
        };
    }

    /// <summary>
    /// Pasta de armazenamento do tipo (primeiro segmento do caminho relativo), por exemplo <c>profile_photo</c>.
    /// </summary>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <returns>O nome da pasta, em minúsculas.</returns>
    public static string Folder(FileKind kind)
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(kind.ToString());
    }

    /// <summary>
    /// Nome do tipo na rota e nos URLs, por exemplo <c>PROFILE_PHOTO</c>.
    /// </summary>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <returns>O nome em maiúsculas com <c>_</c>.</returns>
    public static string RouteName(FileKind kind)
    {
        return JsonNamingPolicy.SnakeCaseUpper.ConvertName(kind.ToString());
    }

    /// <summary>
    /// Reconhece o tipo pelo nome da rota, só com a forma exata (<c>PROFILE_PHOTO</c>).
    /// </summary>
    /// <param name="routeName">Valor de <c>kind</c> na rota.</param>
    /// <param name="kind">O tipo, se o nome for conhecido.</param>
    /// <returns><see langword="true"/> se o nome corresponder a um tipo.</returns>
    public static bool TryParseRouteName(string? routeName, out FileKind kind)
    {
        foreach (FileKind candidate in Enum.GetValues<FileKind>())
        {
            if (string.Equals(RouteName(candidate), routeName, StringComparison.Ordinal))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    /// <summary>
    /// Reconhece o tipo pela pasta do caminho relativo.
    /// </summary>
    /// <param name="folder">Primeiro segmento do caminho relativo.</param>
    /// <param name="kind">O tipo, se a pasta for conhecida.</param>
    /// <returns><see langword="true"/> se a pasta corresponder a um tipo.</returns>
    public static bool TryParseFolder(string? folder, out FileKind kind)
    {
        foreach (FileKind candidate in Enum.GetValues<FileKind>())
        {
            if (string.Equals(Folder(candidate), folder, StringComparison.Ordinal))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }
}
