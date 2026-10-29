// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Implementação de <see cref="IFileStorage"/> sobre o sistema de ficheiros: uma pasta por tipo dentro da raiz
/// configurada. Só aceita caminhos <c>&lt;pasta do tipo&gt;/&lt;nome gerado&gt;</c>, o que impede qualquer percurso de
/// diretório (RNF007).
/// </summary>
public sealed partial class LocalFileStorage : IFileStorage
{
    private readonly string root;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileStorage"/> class.
    /// </summary>
    /// <param name="options">Configuração do armazenamento.</param>
    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.root = options.Value.ResolveRootPath();
    }

    /// <summary>
    /// Indica se o nome tem a forma dos nomes gerados pelo servidor: 32 algarismos hexadecimais e uma extensão aceite.
    /// </summary>
    /// <param name="fileName">Nome do ficheiro, sem pasta.</param>
    /// <returns><see langword="true"/> se tiver a forma de um nome gerado.</returns>
    public static bool IsGeneratedName(string? fileName)
    {
        return fileName is not null && GeneratedNamePattern().IsMatch(fileName);
    }

    /// <inheritdoc/>
    public async Task<string> SaveAsync(Stream content, FileKind kind, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        byte[] header = new byte[FileRules.SignatureLength];
        int read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        FileFormat format = FileRules.Detect(header.AsSpan(0, read));
        if (!FileRules.IsAccepted(kind, format))
        {
            throw new ArgumentException("O conteúdo não é de um formato aceite para este tipo de ficheiro.", nameof(content));
        }

        string folder = FileRules.Folder(kind);
        string fileName = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + FileRules.Extension(format);
        string fullPath = this.Resolve($"{folder}/{fileName}");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        FileStream target = new(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await using (target)
        {
            await target.WriteAsync(header.AsMemory(0, read), cancellationToken);
            await content.CopyToAsync(target, cancellationToken);
        }

        return $"{folder}/{fileName}";
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        string fullPath;
        try
        {
            fullPath = this.Resolve(path);
        }
        catch (ArgumentException)
        {
            throw new FileNotFoundException("O ficheiro não existe.");
        }

        try
        {
            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return Task.FromResult(stream);
        }
        catch (DirectoryNotFoundException)
        {
            throw new FileNotFoundException("O ficheiro não existe.");
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        File.Delete(this.Resolve(path));
        return Task.CompletedTask;
    }

    [GeneratedRegex(@"^[0-9a-f]{32}\.(jpg|png|pdf)\z")]
    private static partial Regex GeneratedNamePattern();

    private string Resolve(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string[] segments = path.Split('/');
        if (segments.Length != 2 || !FileRules.TryParseFolder(segments[0], out _) || !IsGeneratedName(segments[1]))
        {
            throw new ArgumentException("O caminho do ficheiro não é válido.", nameof(path));
        }

        string folderPath = Path.GetFullPath(Path.Combine(this.root, segments[0]));
        string fullPath = Path.GetFullPath(Path.Combine(folderPath, segments[1]));
        if (!fullPath.StartsWith(folderPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("O caminho do ficheiro sai da pasta de armazenamento.", nameof(path));
        }

        return fullPath;
    }
}
