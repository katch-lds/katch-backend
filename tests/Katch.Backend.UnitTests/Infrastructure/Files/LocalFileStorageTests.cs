// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Files;
using Microsoft.Extensions.Options;

namespace Katch.Backend.UnitTests.Infrastructure.Files;

/// <summary>
/// Testes do <see cref="LocalFileStorage"/>, numa pasta temporária por teste que é apagada no fim.
/// </summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];

    private readonly string root = Path.Combine(Path.GetTempPath(), "katch-unit-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileStorageTests"/> class.
    /// </summary>
    public LocalFileStorageTests()
    {
        this.storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = this.root }));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>
    /// Guarda na pasta do tipo, cria as pastas em falta, gera o nome e usa a extensão do formato real.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SavesWithAGeneratedNameAndTheRealExtension()
    {
        string path = await this.storage.SaveAsync(new MemoryStream(Png), FileKind.ProfilePhoto, TestContext.Current.CancellationToken);

        Assert.Matches("^profile_photo/[0-9a-f]{32}\\.png$", path);
        Assert.Equal(Png, await File.ReadAllBytesAsync(Path.Combine(this.root, path), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Dois ficheiros iguais têm nomes diferentes e o PDF fica na pasta do curriculum vitae.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GeneratesDifferentNamesAndOneFolderPerKind()
    {
        string first = await this.storage.SaveAsync(new MemoryStream(Png), FileKind.JobPhoto, TestContext.Current.CancellationToken);
        string second = await this.storage.SaveAsync(new MemoryStream(Png), FileKind.JobPhoto, TestContext.Current.CancellationToken);
        string cv = await this.storage.SaveAsync(new MemoryStream(Pdf), FileKind.CurriculumVitae, TestContext.Current.CancellationToken);

        Assert.NotEqual(first, second);
        Assert.StartsWith("curriculum_vitae/", cv, StringComparison.Ordinal);
        Assert.EndsWith(".pdf", cv, StringComparison.Ordinal);
    }

    /// <summary>
    /// Um conteúdo que não é de um formato aceite para o tipo não é guardado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectsContentOfAnotherFormat()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => this.storage.SaveAsync(new MemoryStream(Pdf), FileKind.ProfilePhoto, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Lê o ficheiro guardado e elimina-o; eliminar o que já não existe não é erro.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpensAndDeletes()
    {
        string path = await this.storage.SaveAsync(new MemoryStream(Png), FileKind.CompanyLogo, TestContext.Current.CancellationToken);

        await using (Stream stream = await this.storage.OpenReadAsync(path, TestContext.Current.CancellationToken))
        {
            Assert.Equal(Png.Length, stream.Length);
        }

        await this.storage.DeleteAsync(path, TestContext.Current.CancellationToken);
        await this.storage.DeleteAsync(path, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<FileNotFoundException>(() => this.storage.OpenReadAsync(path, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Nenhum caminho sai da pasta do tipo: percursos de diretório, separadores, rotas absolutas e nomes não gerados.
    /// </summary>
    /// <param name="path">Caminho malicioso.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("../segredo.png")]
    [InlineData("profile_photo/../../segredo.png")]
    [InlineData("profile_photo/..\\segredo.png")]
    [InlineData("profile_photo/a/b.png")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("profile_photo/nome-escolhido.png")]
    [InlineData("desconhecido/00000000000000000000000000000000.png")]
    [InlineData("profile_photo")]
    [InlineData("")]
    public async Task NeverLeavesTheTypeFolder(string path)
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => this.storage.OpenReadAsync(path, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => this.storage.DeleteAsync(path, TestContext.Current.CancellationToken));
    }
}
