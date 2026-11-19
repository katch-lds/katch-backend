// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Controllers.Mapping;
using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Files;

namespace Katch.Backend.UnitTests.Controllers.Mapping;

/// <summary>
/// Testes do <see cref="CompanyPageMapper"/>, que constrói na camada da API a página da Empresa a partir da entidade
/// devolvida pelos serviços (I138): os caminhos guardados do logótipo e da galeria são convertidos nos URL da API, pela
/// ordem, e os contactos e os dados do registo nunca fazem parte da página (6.3.3, 6.4.4, 6.4.9; RF017, RF025).
/// </summary>
public sealed class CompanyPageMapperTests
{
    private readonly FileUrlBuilder fileUrlBuilder = new();

    /// <summary>
    /// A página traz a designação, a descrição, o sítio na Internet e os URL da API do logótipo e da galeria.
    /// </summary>
    [Fact]
    public void PageHasTheDataAndTheUrlsOfTheFiles()
    {
        Company company = ServiceModels.Company;
        company.AddGalleryPhoto("company_photo/22222222222222222222222222222222.png");

        CompanyPageDto page = company.ToPageDto(this.fileUrlBuilder);

        Assert.Equal(company.Id, page.CompanyId);
        Assert.Equal("Lumen Software, Lda.", page.CompanyName);
        Assert.Equal("Software de gestão de frotas.", page.Description);
        Assert.Equal("https://www.lumen-exemplo.pt", page.Website);
        Assert.Equal("/api/files/COMPANY_LOGO/0123456789abcdef0123456789abcdef.png", page.LogoUrl);
        Assert.Equal(
            [
                "/api/files/COMPANY_PHOTO/11111111111111111111111111111111.jpg",
                "/api/files/COMPANY_PHOTO/22222222222222222222222222222222.png",
            ],
            page.PhotoUrls);
    }

    /// <summary>
    /// Sem página, logótipo nem fotografias, a descrição, o sítio e o URL do logótipo são nulos e a galeria é vazia.
    /// </summary>
    [Fact]
    public void PageWithoutFilesHasNoUrls()
    {
        Company company = ServiceModels.ApprovedCompany;

        CompanyPageDto page = company.ToPageDto(this.fileUrlBuilder);

        Assert.Null(page.Description);
        Assert.Null(page.Website);
        Assert.Null(page.LogoUrl);
        Assert.Empty(page.PhotoUrls);
    }

    /// <summary>
    /// A página só tem os campos públicos da Empresa: nunca os contactos, o NIF, a morada nem o responsável.
    /// </summary>
    [Fact]
    public void PageNeverHasTheContactsOfTheCompany()
    {
        Assert.Equal(
            ["CompanyId", "CompanyName", "Description", "LogoUrl", "PhotoUrls", "Website"],
            typeof(CompanyPageDto).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A conversão recusa a Empresa ou a conversão dos caminhos nulas.
    /// </summary>
    [Fact]
    public void NullArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ((Company)null!).ToPageDto(this.fileUrlBuilder));
        Assert.Throws<ArgumentNullException>(() => ServiceModels.Company.ToPageDto(null!));
    }
}
