// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Infrastructure.Files;

namespace Katch.Backend.UnitTests.Infrastructure.Errors;

/// <summary>
/// Testes das designações usadas nas mensagens do catálogo de erros (estado da Empresa no ERR-07 e estado da vaga no
/// ERR-30; documentação da API v02, secções 2.4 e 4) e das conversões de <see cref="FileRules"/> usadas nas respostas
/// dos ficheiros.
/// </summary>
public sealed class StatusLabelsTests
{
    /// <summary>
    /// Cada estado da Empresa tem a sua designação; sem Empresa, «não registada».
    /// </summary>
    /// <param name="status">Estado da Empresa.</param>
    /// <param name="label">Designação esperada.</param>
    [Theory]
    [InlineData(null, "não registada")]
    [InlineData(CompanyStatus.Pending, "pendente")]
    [InlineData(CompanyStatus.Approved, "aprovada")]
    [InlineData(CompanyStatus.Rejected, "recusada")]
    [InlineData(CompanyStatus.Suspended, "suspensa")]
    public void CompanyStatusLabels(CompanyStatus? status, string label)
    {
        Assert.Equal(label, ApiErrorResponse.CompanyStatusLabel(status));
        Assert.Equal(
            $"A operação exige a Empresa aprovada. Estado atual da Empresa: {label}.",
            ApiErrorResponse.CompanyNotApprovedMessage(status));
    }

    /// <summary>
    /// Cada estado da vaga tem a sua designação no ERR-30.
    /// </summary>
    /// <param name="status">Estado da vaga.</param>
    /// <param name="label">Designação esperada.</param>
    [Theory]
    [InlineData(JobStatus.Unpublished, "não publicada")]
    [InlineData(JobStatus.Published, "publicada")]
    [InlineData(JobStatus.Suspended, "suspensa")]
    [InlineData(JobStatus.Closed, "encerrada")]
    public void JobStatusLabels(JobStatus status, string label)
    {
        Assert.Equal(label, ApiErrorResponse.JobStatusLabel(status));
    }

    /// <summary>
    /// Um estado fora das enumerações é um erro de programação e não é mostrado ao utilizador.
    /// </summary>
    [Fact]
    public void UnknownStatusesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ApiErrorResponse.CompanyStatusLabel((CompanyStatus)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApiErrorResponse.JobStatusLabel((JobStatus)99));
    }

    /// <summary>
    /// Cada formato tem o tipo de conteúdo e a extensão da resposta; o formato desconhecido é recusado.
    /// </summary>
    [Fact]
    public void FileFormatsMapToContentTypesAndExtensions()
    {
        Assert.Equal("image/jpeg", FileRules.ContentType(FileFormat.Jpeg));
        Assert.Equal("image/png", FileRules.ContentType(FileFormat.Png));
        Assert.Equal("application/pdf", FileRules.ContentType(FileFormat.Pdf));
        Assert.Equal(".jpg", FileRules.Extension(FileFormat.Jpeg));
        Assert.Throws<ArgumentOutOfRangeException>(() => FileRules.ContentType(FileFormat.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => FileRules.Extension(FileFormat.Unknown));
    }

    /// <summary>
    /// O nome da rota de cada tipo de ficheiro é reconhecido só na forma exata; outros valores e <see langword="null"/>
    /// não são reconhecidos.
    /// </summary>
    [Fact]
    public void RouteNamesRoundTripOnlyInTheExactForm()
    {
        foreach (FileKind kind in Enum.GetValues<FileKind>())
        {
            Assert.True(FileRules.TryParseRouteName(FileRules.RouteName(kind), out FileKind parsed));
            Assert.Equal(kind, parsed);
        }

        Assert.False(FileRules.TryParseRouteName("job_photo", out _));
        Assert.False(FileRules.TryParseRouteName(null, out _));
    }
}
