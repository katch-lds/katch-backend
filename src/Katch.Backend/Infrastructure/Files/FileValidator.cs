// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Errors;

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Implementação de <see cref="IFileValidator"/>. O formato real é o dos primeiros bytes do conteúdo e tem de coincidir
/// com o que a extensão do nome indica: um <c>.png</c> com conteúdo de outro formato é recusado (RNF016).
/// </summary>
public sealed class FileValidator : IFileValidator
{
    /// <inheritdoc/>
    public ValidationResult Validate(IFormFile? file, FileKind kind)
    {
        if (file is null || file.Length == 0)
        {
            return ValidationResult.Invalid(FieldReasons.Required);
        }

        string wrongFormat = kind == FileKind.CurriculumVitae ? FieldReasons.FileMustBePdf : FieldReasons.FileMustBeImage;

        FileFormat byExtension = FileRules.FromExtension(Path.GetExtension(file.FileName));
        if (!FileRules.IsAccepted(kind, byExtension))
        {
            return ValidationResult.Invalid(wrongFormat);
        }

        byte[] header = new byte[FileRules.SignatureLength];
        int read;
        using (Stream stream = file.OpenReadStream())
        {
            read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        }

        if (FileRules.Detect(header.AsSpan(0, read)) != byExtension)
        {
            return ValidationResult.Invalid(wrongFormat);
        }

        int maxBytes = FileRules.MaxBytes(kind);
        return file.Length > maxBytes
            ? ValidationResult.Invalid(FieldReasons.FileTooLarge(maxBytes / FileRules.Megabyte))
            : ValidationResult.Valid;
    }
}
