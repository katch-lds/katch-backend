// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Files;

namespace Katch.Backend.UnitTests.Infrastructure.Files;

/// <summary>
/// Testes do <see cref="FileUrlBuilder"/>.
/// </summary>
public sealed class FileUrlBuilderTests
{
    private readonly FileUrlBuilder builder = new();

    /// <summary>
    /// O caminho <c>&lt;tipo&gt;/&lt;nome&gt;</c> passa a <c>/api/files/&lt;TIPO&gt;/&lt;nome&gt;</c>.
    /// </summary>
    /// <param name="path">Caminho guardado.</param>
    /// <param name="url">URL esperado.</param>
    [Theory]
    [InlineData("profile_photo/abc.png", "/api/files/PROFILE_PHOTO/abc.png")]
    [InlineData("company_logo/abc.jpg", "/api/files/COMPANY_LOGO/abc.jpg")]
    [InlineData("company_photo/abc.jpg", "/api/files/COMPANY_PHOTO/abc.jpg")]
    [InlineData("job_photo/abc.png", "/api/files/JOB_PHOTO/abc.png")]
    public void ConvertsThePathIntoTheApiUrl(string path, string url)
    {
        Assert.Equal(url, this.builder.ToUrl(path));
    }

    /// <summary>
    /// Sem caminho não há URL.
    /// </summary>
    [Fact]
    public void NullPathGivesNullUrl()
    {
        Assert.Null(this.builder.ToUrl(null));
    }

    /// <summary>
    /// Vários caminhos são convertidos pela mesma ordem.
    /// </summary>
    [Fact]
    public void ConvertsSeveralPathsInOrder()
    {
        Assert.Equal(
            ["/api/files/JOB_PHOTO/b.png", "/api/files/JOB_PHOTO/a.png"],
            this.builder.ToUrls(["job_photo/b.png", "job_photo/a.png"]));
    }

    /// <summary>
    /// Um caminho que não tem a forma <c>&lt;tipo&gt;/&lt;nome&gt;</c> não é convertido.
    /// </summary>
    /// <param name="path">Caminho inválido.</param>
    [Theory]
    [InlineData("abc.png")]
    [InlineData("desconhecido/abc.png")]
    [InlineData("job_photo/a/b.png")]
    public void InvalidPathIsRejected(string path)
    {
        Assert.Throws<ArgumentException>(() => this.builder.ToUrl(path));
    }
}
