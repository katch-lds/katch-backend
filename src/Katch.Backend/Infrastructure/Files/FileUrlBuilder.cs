// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Implementação de <see cref="IFileUrlBuilder"/>.
/// </summary>
public sealed class FileUrlBuilder : IFileUrlBuilder
{
    /// <inheritdoc/>
    public string? ToUrl(string? path)
    {
        if (path is null)
        {
            return null;
        }

        string[] segments = path.Split('/');
        if (segments.Length != 2 || !FileRules.TryParseFolder(segments[0], out var kind))
        {
            throw new ArgumentException("O caminho do ficheiro não tem o formato <tipo>/<nome>.", nameof(path));
        }

        return $"/api/files/{FileRules.RouteName(kind)}/{segments[1]}";
    }

    /// <inheritdoc/>
    public List<string> ToUrls(List<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return paths.Select(path => this.ToUrl(path)!).ToList();
    }
}
