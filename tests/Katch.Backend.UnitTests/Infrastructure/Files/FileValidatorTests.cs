// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Files;
using Microsoft.AspNetCore.Http;

namespace Katch.Backend.UnitTests.Infrastructure.Files;

/// <summary>
/// Testes do <see cref="FileValidator"/>: extensão, assinatura do conteúdo e dimensão (RNF015, RNF016).
/// </summary>
public sealed class FileValidatorTests
{
    private const string ImageReason = "O ficheiro tem de estar no formato JPEG ou PNG.";
    private const string PdfReason = "O ficheiro tem de estar no formato PDF.";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00];
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];

    private readonly FileValidator validator = new();

    /// <summary>
    /// Os formatos certos, com a extensão certa, são aceites.
    /// </summary>
    /// <param name="fileName">Nome do ficheiro.</param>
    /// <param name="format">Conteúdo: png, jpeg ou pdf.</param>
    /// <param name="kind">Tipo de ficheiro.</param>
    [Theory]
    [InlineData("a.png", "png", FileKind.ProfilePhoto)]
    [InlineData("A.PNG", "png", FileKind.CompanyPhoto)]
    [InlineData("a.jpg", "jpeg", FileKind.JobPhoto)]
    [InlineData("a.jpeg", "jpeg", FileKind.CompanyLogo)]
    [InlineData("cv.pdf", "pdf", FileKind.CurriculumVitae)]
    public void AcceptsTheRightFormats(string fileName, string format, FileKind kind)
    {
        Assert.True(this.validator.Validate(File(fileName, Content(format)), kind).IsValid);
    }

    /// <summary>
    /// A extensão errada, o conteúdo de outro formato, o GIF e o DOCX são recusados com o motivo do formato.
    /// </summary>
    /// <param name="fileName">Nome do ficheiro.</param>
    /// <param name="format">Conteúdo.</param>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <param name="reason">Motivo esperado.</param>
    [Theory]
    [InlineData("foto.png", "text", FileKind.ProfilePhoto, ImageReason)]
    [InlineData("foto.png", "pdf", FileKind.ProfilePhoto, ImageReason)]
    [InlineData("foto.jpg", "png", FileKind.ProfilePhoto, ImageReason)]
    [InlineData("foto.png", "jpeg", FileKind.CompanyLogo, ImageReason)]
    [InlineData("foto.gif", "gif", FileKind.JobPhoto, ImageReason)]
    [InlineData("foto.png", "gif", FileKind.JobPhoto, ImageReason)]
    [InlineData("foto.docx", "docx", FileKind.CompanyPhoto, ImageReason)]
    [InlineData("foto", "png", FileKind.ProfilePhoto, ImageReason)]
    [InlineData("cv.pdf", "png", FileKind.CurriculumVitae, PdfReason)]
    [InlineData("cv.pdf", "text", FileKind.CurriculumVitae, PdfReason)]
    [InlineData("cv.png", "pdf", FileKind.CurriculumVitae, PdfReason)]
    [InlineData("cv.docx", "docx", FileKind.CurriculumVitae, PdfReason)]
    [InlineData("foto.pdf", "pdf", FileKind.ProfilePhoto, ImageReason)]
    public void RejectsTheWrongFormats(string fileName, string format, FileKind kind, string reason)
    {
        ValidationResult result = this.validator.Validate(File(fileName, Content(format)), kind);

        Assert.False(result.IsValid);
        Assert.Equal(reason, result.Reason);
    }

    /// <summary>
    /// A dimensão máxima inclui o limite e recusa um byte a mais: 5 MB, ou 2 MB no logótipo.
    /// </summary>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <param name="fileName">Nome do ficheiro.</param>
    /// <param name="format">Conteúdo.</param>
    /// <param name="megabytes">Limite em megabytes.</param>
    [Theory]
    [InlineData(FileKind.ProfilePhoto, "a.png", "png", 5)]
    [InlineData(FileKind.CurriculumVitae, "a.pdf", "pdf", 5)]
    [InlineData(FileKind.CompanyPhoto, "a.jpg", "jpeg", 5)]
    [InlineData(FileKind.JobPhoto, "a.png", "png", 5)]
    [InlineData(FileKind.CompanyLogo, "a.png", "png", 2)]
    public void SizeLimitIsInclusive(FileKind kind, string fileName, string format, int megabytes)
    {
        int limit = megabytes * FileRules.Megabyte;
        byte[] header = Content(format);

        Assert.True(this.validator.Validate(File(fileName, Sized(header, limit)), kind).IsValid);
        ValidationResult over = this.validator.Validate(File(fileName, Sized(header, limit + 1)), kind);
        Assert.False(over.IsValid);
        Assert.Equal($"O ficheiro excede {megabytes} MB.", over.Reason);
    }

    /// <summary>
    /// Sem ficheiro, ou com um ficheiro vazio, o campo é obrigatório.
    /// </summary>
    [Fact]
    public void MissingOrEmptyFileIsRequired()
    {
        Assert.Equal("O campo é obrigatório.", this.validator.Validate(null, FileKind.ProfilePhoto).Reason);
        Assert.Equal("O campo é obrigatório.", this.validator.Validate(File("a.png", []), FileKind.ProfilePhoto).Reason);
    }

    private static FormFile File(string fileName, byte[] content)
    {
        return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName);
    }

    private static byte[] Sized(byte[] header, int length)
    {
        byte[] content = new byte[length];
        header.CopyTo(content, 0);
        return content;
    }

    private static byte[] Content(string format)
    {
        return format switch
        {
            "png" => Png,
            "jpeg" => Jpeg,
            "pdf" => Pdf,
            "gif" => "GIF89a"u8.ToArray(),
            "docx" => [0x50, 0x4B, 0x03, 0x04],
            _ => "Só texto"u8.ToArray(),
        };
    }
}
