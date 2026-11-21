// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Buffers.Binary;
using System.Text;

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Retira das imagens os metadados e o conteúdo que não fazem parte da imagem (SEG-010), sem as voltar a codificar:
/// nos JPEG, os segmentos APP1 a APP13, APP15 e COM (EXIF com a localização GPS, XMP, IPTC, comentários e imagens
/// embutidas) e o que vier depois do fim da imagem; nos PNG, os blocos de texto, de EXIF e de data e o que vier depois
/// do bloco final. Ficam o APP0 (JFIF) e o APP14 (Adobe), de que a descodificação das cores depende. Um ficheiro com a
/// estrutura inválida é devolvido como chegou: a assinatura já foi validada e um ficheiro truncado não tem metadados a
/// mais do que os que se conseguem ler.
/// </summary>
public static class ImageMetadata
{
    private const byte Marker = 0xFF;
    private const byte StartOfScan = 0xDA;
    private const byte EndOfImage = 0xD9;
    private const byte App0 = 0xE0;
    private const byte App14 = 0xEE;
    private const byte App15 = 0xEF;
    private const byte Comment = 0xFE;
    private const int PngSignatureLength = 8;

    private static readonly string[] PngMetadataChunks = ["eXIf", "tEXt", "zTXt", "iTXt", "tIME"];

    /// <summary>
    /// Devolve a imagem sem os metadados; os PDF e os formatos desconhecidos ficam como estão.
    /// </summary>
    /// <param name="content">Conteúdo do ficheiro.</param>
    /// <param name="format">Formato real do conteúdo.</param>
    /// <returns>O conteúdo a guardar.</returns>
    public static byte[] Strip(byte[] content, FileFormat format)
    {
        ArgumentNullException.ThrowIfNull(content);

        byte[]? stripped = format switch
        {
            FileFormat.Jpeg => StripJpeg(content),
            FileFormat.Png => StripPng(content),
            _ => content,
        };
        return stripped ?? content;
    }

    private static byte[]? StripJpeg(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 2);
        int position = 2;

        while (position + 1 < data.Length)
        {
            if (data[position] != Marker)
            {
                return null;
            }

            byte type = data[position + 1];
            if (type == Marker)
            {
                position++;
                continue;
            }

            if (type == EndOfImage)
            {
                output.Write(data, position, 2);
                return output.ToArray();
            }

            if (position + 3 >= data.Length)
            {
                return null;
            }

            int length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 2, 2));
            int end = position + 2 + length;
            if (length < 2 || end > data.Length)
            {
                return null;
            }

            if (!IsJpegMetadata(type))
            {
                output.Write(data, position, end - position);
            }

            position = type == StartOfScan ? CopyScan(data, end, output) : end;
        }

        return null;
    }

    private static bool IsJpegMetadata(byte type)
    {
        return type == Comment || (type > App0 && type <= App15 && type != App14);
    }

    /// <summary>
    /// Copia os dados comprimidos de um scan até ao marcador seguinte, ignorando os bytes de enchimento (<c>FF 00</c>)
    /// e os marcadores de reinício (<c>FF D0</c> a <c>FF D7</c>), que fazem parte dos dados.
    /// </summary>
    private static int CopyScan(byte[] data, int start, MemoryStream output)
    {
        int position = start;
        while (position + 1 < data.Length
            && !(data[position] == Marker && data[position + 1] != 0x00 && data[position + 1] is < 0xD0 or > 0xD7))
        {
            position++;
        }

        output.Write(data, start, position - start);
        return position;
    }

    private static byte[]? StripPng(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, PngSignatureLength);
        int position = PngSignatureLength;

        while (position + 8 <= data.Length)
        {
            long length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position, 4));
            long end = position + 12 + length;
            if (end > data.Length)
            {
                return null;
            }

            string type = Encoding.ASCII.GetString(data, position + 4, 4);
            if (!PngMetadataChunks.Contains(type, StringComparer.Ordinal))
            {
                output.Write(data, position, (int)(end - position));
            }

            if (type == "IEND")
            {
                return output.ToArray();
            }

            position = (int)end;
        }

        return null;
    }
}
