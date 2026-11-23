// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Buffers.Binary;
using System.Text;
using Katch.Backend.Infrastructure.Files;

namespace Katch.Backend.UnitTests.Infrastructure.Files;

/// <summary>
/// Testes da remoção dos metadados das imagens (SEG-010), com ficheiros construídos segmento a segmento.
/// </summary>
public sealed class ImageMetadataTests
{
    private static readonly byte[] Jfif = Segment(0xE0, "JFIF\0\u0001\u0002");
    private static readonly byte[] Exif = Segment(0xE1, "Exif\0\0GPS 41.5N 8.4W");
    private static readonly byte[] Icc = Segment(0xE2, "ICC_PROFILE");
    private static readonly byte[] Adobe = Segment(0xEE, "Adobe");
    private static readonly byte[] CommentSegment = Segment(0xFE, "telemovel da Ana");
    private static readonly byte[] Quantization = Segment(0xDB, "\0quantizacao");
    private static readonly byte[] Scan = Segment(0xDA, "\u0001\u0001\0");
    private static readonly byte[] ScanData = [0x12, 0xFF, 0x00, 0x34, 0xFF, 0xD3, 0x56];

    /// <summary>
    /// Num JPEG saem o EXIF, os outros APPn, os comentários e o que vier depois do fim da imagem; ficam o JFIF, o Adobe,
    /// as tabelas e os dados comprimidos, incluindo o enchimento e os marcadores de reinício.
    /// </summary>
    [Fact]
    public void JpegLosesMetadataAndTrailingContent()
    {
        byte[] jpeg = Concat([0xFF, 0xD8], Jfif, Exif, Icc, Adobe, CommentSegment, Quantization, Scan, ScanData, [0xFF, 0xD9], Ascii("PAYLOAD"));

        byte[] stripped = ImageMetadata.Strip(jpeg, FileFormat.Jpeg);

        Assert.Equal(Concat([0xFF, 0xD8], Jfif, Adobe, Quantization, Scan, ScanData, [0xFF, 0xD9]), stripped);
    }

    /// <summary>
    /// Num JPEG progressivo, os segmentos entre dois scans continuam a ser lidos e os metadados depois do primeiro scan
    /// também saem.
    /// </summary>
    [Fact]
    public void ProgressiveJpegKeepsEverySegmentBetweenScans()
    {
        byte[] huffman = Segment(0xC4, "\0tabela");
        byte[] jpeg = Concat([0xFF, 0xD8], Scan, ScanData, huffman, CommentSegment, Scan, ScanData, [0xFF, 0xD9]);

        byte[] stripped = ImageMetadata.Strip(jpeg, FileFormat.Jpeg);

        Assert.Equal(Concat([0xFF, 0xD8], Scan, ScanData, huffman, Scan, ScanData, [0xFF, 0xD9]), stripped);
    }

    /// <summary>
    /// Num PNG saem os blocos de EXIF, texto e data e o que vier depois do IEND; ficam os restantes.
    /// </summary>
    [Fact]
    public void PngLosesMetadataChunksAndTrailingContent()
    {
        byte[] header = Chunk("IHDR", new byte[13]);
        byte[] physical = Chunk("pHYs", new byte[9]);
        byte[] image = Chunk("IDAT", [1, 2, 3]);
        byte[] end = Chunk("IEND", []);
        byte[] png = Concat(
            PngSignature(),
            header,
            Chunk("eXIf", Ascii("GPS")),
            Chunk("tEXt", Ascii("Author\0Ana")),
            Chunk("iTXt", Ascii("XML:com.adobe.xmp")),
            Chunk("zTXt", Ascii("Comment")),
            Chunk("tIME", new byte[7]),
            physical,
            image,
            end,
            Ascii("PAYLOAD"));

        byte[] stripped = ImageMetadata.Strip(png, FileFormat.Png);

        Assert.Equal(Concat(PngSignature(), header, physical, image, end), stripped);
    }

    /// <summary>
    /// Um ficheiro com a estrutura inválida e um PDF ficam como chegaram.
    /// </summary>
    /// <param name="format">Formato indicado.</param>
    [Theory]
    [InlineData(FileFormat.Jpeg)]
    [InlineData(FileFormat.Png)]
    [InlineData(FileFormat.Pdf)]
    public void InvalidStructureOrPdfIsKept(FileFormat format)
    {
        byte[] content = Concat([0xFF, 0xD8, 0xFF, 0xE1, 0x7F, 0xFF], Ascii("truncado"));

        Assert.Same(content, ImageMetadata.Strip(content, format));
    }

    private static byte[] Segment(byte type, string payload)
    {
        byte[] data = Encoding.Latin1.GetBytes(payload);
        byte[] segment = new byte[data.Length + 4];
        segment[0] = 0xFF;
        segment[1] = type;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(data.Length + 2));
        data.CopyTo(segment, 4);
        return segment;
    }

    private static byte[] Chunk(string type, byte[] data)
    {
        byte[] chunk = new byte[data.Length + 12];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)data.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(chunk, 4);
        data.CopyTo(chunk, 8);
        return chunk;
    }

    private static byte[] PngSignature()
    {
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    }

    private static byte[] Ascii(string text)
    {
        return Encoding.ASCII.GetBytes(text);
    }

    private static byte[] Concat(params byte[][] parts)
    {
        return parts.SelectMany(part => part).ToArray();
    }
}
