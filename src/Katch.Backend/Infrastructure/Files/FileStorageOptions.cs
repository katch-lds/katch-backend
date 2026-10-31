// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Configuração do armazenamento de ficheiros (secção <c>FileStorage</c>).
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>
    /// Nome da secção de configuração.
    /// </summary>
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Gets or sets a pasta raiz dos ficheiros guardados. Quando não é indicada, usa-se uma pasta da conta do
    /// utilizador do sistema, fora do repositório de código.
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>
    /// Pasta raiz efetiva, em caminho absoluto.
    /// </summary>
    /// <returns>O valor configurado ou a pasta por omissão.</returns>
    public string ResolveRootPath()
    {
        string root = string.IsNullOrWhiteSpace(this.RootPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Katch", "files")
            : this.RootPath;
        return Path.GetFullPath(root);
    }
}
