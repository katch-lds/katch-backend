// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="Company"/>: registo (RF041, RF043), nova submissão (RF045), alteração dos dados de
/// registo (RF049), aprovação, recusa, suspensão e reativação (RF093 a RF095, RF116) e página de apresentação (RF046 a
/// RF048).
/// </summary>
public sealed class CompanyTests
{
    private static readonly DateTimeOffset Submitted = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid RecruiterId = Guid.NewGuid();

    private static readonly Guid LocationId = Guid.NewGuid();

    /// <summary>
    /// Uma Empresa nova fica pendente, com os dados de registo, a data de submissão e a página vazia.
    /// </summary>
    [Fact]
    public void NewCompanyIsPendingWithTheRegistrationData()
    {
        CompanyRegistration data = ValidData();

        var company = new Company(RecruiterId, data, Submitted);

        Assert.NotEqual(Guid.Empty, company.Id);
        Assert.Equal(RecruiterId, company.RecruiterId);
        Assert.Equal("Lumen Software, Lda.", company.CompanyName);
        Assert.Equal("509123456", company.TaxId);
        Assert.Equal(Industry.Technology, company.Industry);
        Assert.Equal("Avenida da Liberdade, 100, 4710-000 Braga", company.Address);
        Assert.Equal(LocationId, company.LocationId);
        Assert.Equal("geral@lumen-exemplo.pt", company.ContactEmail);
        Assert.Equal("253000111", company.ContactPhone);
        Assert.Equal("Rui Matos", company.ResponsibleName);
        Assert.Equal(CompanyStatus.Pending, company.Status);
        Assert.Null(company.StatusReason);
        Assert.Equal(Submitted, company.SubmittedAt);
        Assert.Null(company.Website);
        Assert.Null(company.Description);
        Assert.Null(company.LogoPath);
        Assert.Empty(company.PhotoPaths);
        Assert.False(company.IsApproved());
    }

    /// <summary>
    /// O Recrutador e os dados de registo são obrigatórios.
    /// </summary>
    [Fact]
    public void MissingRecruiterOrDataIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Company(Guid.Empty, ValidData(), Submitted));
        Assert.Throws<ArgumentNullException>(() => new Company(RecruiterId, null!, Submitted));
    }

    /// <summary>
    /// O NIF e o telefone de contacto têm exatamente 9 algarismos (P04).
    /// </summary>
    /// <param name="value">Valor inválido.</param>
    [Theory]
    [InlineData("")]
    [InlineData("50912345")]
    [InlineData("5091234567")]
    [InlineData("50912345a")]
    [InlineData("509123456\n")]
    [InlineData(" 509123456")]
    public void TaxIdOrPhoneWithoutNineDigitsIsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { TaxId = value }, Submitted));
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { ContactPhone = value }, Submitted));
    }

    /// <summary>
    /// O correio eletrónico de contacto tem o formato local@domínio, sem espaços (P04).
    /// </summary>
    /// <param name="email">Endereço inválido.</param>
    [Theory]
    [InlineData("")]
    [InlineData("geral")]
    [InlineData("@lumen.pt")]
    [InlineData("geral@")]
    [InlineData("ge@ral@lumen.pt")]
    [InlineData("geral @lumen.pt")]
    public void InvalidContactEmailIsRejected(string email)
    {
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { ContactEmail = email }, Submitted));
    }

    /// <summary>
    /// A designação, a morada e o responsável são obrigatórios e respeitam os comprimentos máximos (DM-12).
    /// </summary>
    [Fact]
    public void EmptyOrTooLongTextIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { CompanyName = "  " }, Submitted));
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { CompanyName = new string('a', 201) }, Submitted));
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { Address = string.Empty }, Submitted));
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { Address = new string('a', 256) }, Submitted));
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { ResponsibleName = " " }, Submitted));
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { ResponsibleName = new string('a', 151) }, Submitted));
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { ContactEmail = new string('a', 251) + "@x.pt" }, Submitted));
    }

    /// <summary>
    /// Os comprimentos máximos são aceites.
    /// </summary>
    [Fact]
    public void MaximumLengthsAreAccepted()
    {
        CompanyRegistration data = ValidData() with
        {
            CompanyName = new string('a', 200),
            Address = new string('b', 255),
            ResponsibleName = new string('c', 150),
            ContactEmail = new string('d', 250) + "@x.pt",
        };

        var company = new Company(RecruiterId, data, Submitted);

        Assert.Equal(255, company.ContactEmail.Length);
    }

    /// <summary>
    /// O setor de atividade tem de pertencer à lista pré-definida (P15).
    /// </summary>
    [Fact]
    public void IndustryOutsideTheListIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Company(RecruiterId, ValidData() with { Industry = (Industry)99 }, Submitted));
    }

    /// <summary>
    /// Uma Empresa pendente é aprovada (RF093).
    /// </summary>
    [Fact]
    public void PendingCompanyIsApproved()
    {
        Company company = NewCompany();

        company.Approve();

        Assert.Equal(CompanyStatus.Approved, company.Status);
        Assert.True(company.IsApproved());
    }

    /// <summary>
    /// Uma Empresa pendente é recusada com o motivo, guardado sem os espaços nas extremidades (RF094, P12).
    /// </summary>
    [Fact]
    public void PendingCompanyIsRejectedWithTheTrimmedReason()
    {
        Company company = NewCompany();

        company.Reject("  A morada não corresponde à certidão.  ");

        Assert.Equal(CompanyStatus.Rejected, company.Status);
        Assert.Equal("A morada não corresponde à certidão.", company.StatusReason);
        Assert.False(company.IsApproved());
    }

    /// <summary>
    /// O motivo da recusa tem entre 10 e 500 caracteres, sem os espaços nas extremidades (P12).
    /// </summary>
    /// <param name="length">Comprimento do motivo.</param>
    /// <param name="accepted">Se o motivo é aceite.</param>
    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void ReasonLengthMustBeBetweenTenAndFiveHundred(int length, bool accepted)
    {
        Company company = NewCompany();
        string reason = new('m', length);

        if (accepted)
        {
            company.Reject(reason);
            Assert.Equal(length, company.StatusReason!.Length);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => company.Reject(reason));
            Assert.Equal(CompanyStatus.Pending, company.Status);
        }
    }

    /// <summary>
    /// Um motivo com menos de 10 caracteres sem os espaços é rejeitado, mesmo que com os espaços chegue aos 10.
    /// </summary>
    [Fact]
    public void ReasonPaddedWithSpacesIsRejected()
    {
        Company company = NewCompany();

        Assert.Throws<ArgumentException>(() => company.Reject("   curto   "));
        Assert.Throws<ArgumentNullException>(() => company.Reject(null!));
    }

    /// <summary>
    /// Só uma Empresa pendente pode ser aprovada ou recusada (documentação da API v02, secção 6.11).
    /// </summary>
    /// <param name="status">Estado que não é pendente.</param>
    [Theory]
    [InlineData(CompanyStatus.Approved)]
    [InlineData(CompanyStatus.Rejected)]
    [InlineData(CompanyStatus.Suspended)]
    public void ApproveAndRejectRequirePending(CompanyStatus status)
    {
        Company company = CompanyIn(status);

        Assert.Throws<InvalidOperationException>(company.Approve);
        Assert.Throws<InvalidOperationException>(() => company.Reject("Motivo suficientemente longo."));
        Assert.Equal(status, company.Status);
    }

    /// <summary>
    /// Uma Empresa aprovada é suspensa (RF095) e reativada, voltando a aprovada (RF116).
    /// </summary>
    [Fact]
    public void ApprovedCompanyIsSuspendedAndReactivated()
    {
        Company company = CompanyIn(CompanyStatus.Approved);

        company.Suspend();
        Assert.Equal(CompanyStatus.Suspended, company.Status);
        Assert.False(company.IsApproved());

        company.Reactivate();
        Assert.Equal(CompanyStatus.Approved, company.Status);
        Assert.True(company.IsApproved());
    }

    /// <summary>
    /// Só uma Empresa aprovada pode ser suspensa.
    /// </summary>
    /// <param name="status">Estado que não é aprovado.</param>
    [Theory]
    [InlineData(CompanyStatus.Pending)]
    [InlineData(CompanyStatus.Rejected)]
    [InlineData(CompanyStatus.Suspended)]
    public void SuspendRequiresApproved(CompanyStatus status)
    {
        Company company = CompanyIn(status);

        Assert.Throws<InvalidOperationException>(company.Suspend);
        Assert.Equal(status, company.Status);
    }

    /// <summary>
    /// Só uma Empresa suspensa pode ser reativada.
    /// </summary>
    /// <param name="status">Estado que não é suspenso.</param>
    [Theory]
    [InlineData(CompanyStatus.Pending)]
    [InlineData(CompanyStatus.Approved)]
    [InlineData(CompanyStatus.Rejected)]
    public void ReactivateRequiresSuspended(CompanyStatus status)
    {
        Company company = CompanyIn(status);

        Assert.Throws<InvalidOperationException>(company.Reactivate);
        Assert.Equal(status, company.Status);
    }

    /// <summary>
    /// A nova submissão de um pedido recusado aplica os dados corrigidos (incluindo o NIF), volta a pendente, limpa o
    /// motivo e atualiza a data de submissão (RF045).
    /// </summary>
    [Fact]
    public void ResubmitCorrectsTheDataAndReturnsToPending()
    {
        Company company = CompanyIn(CompanyStatus.Rejected);
        DateTimeOffset resubmitted = Submitted.AddDays(1);
        CompanyRegistration corrected = ValidData() with { TaxId = "509999999", Address = "Rua Nova, 15, Braga" };

        company.Resubmit(corrected, resubmitted);

        Assert.Equal(CompanyStatus.Pending, company.Status);
        Assert.Null(company.StatusReason);
        Assert.Equal(resubmitted, company.SubmittedAt);
        Assert.Equal("509999999", company.TaxId);
        Assert.Equal("Rua Nova, 15, Braga", company.Address);
    }

    /// <summary>
    /// Só um pedido recusado pode ser submetido novamente, e os dados corrigidos são validados.
    /// </summary>
    /// <param name="status">Estado que não é recusado.</param>
    [Theory]
    [InlineData(CompanyStatus.Pending)]
    [InlineData(CompanyStatus.Approved)]
    [InlineData(CompanyStatus.Suspended)]
    public void ResubmitRequiresRejected(CompanyStatus status)
    {
        Company company = CompanyIn(status);

        Assert.Throws<InvalidOperationException>(() => company.Resubmit(ValidData(), Submitted.AddDays(1)));
        Assert.Equal(Submitted, company.SubmittedAt);
    }

    /// <summary>
    /// Uma nova submissão com dados inválidos não altera a Empresa.
    /// </summary>
    [Fact]
    public void ResubmitWithInvalidDataKeepsTheCompanyRejected()
    {
        Company company = CompanyIn(CompanyStatus.Rejected);

        Assert.Throws<ArgumentException>(() => company.Resubmit(ValidData() with { TaxId = "123" }, Submitted.AddDays(1)));
        Assert.Equal(CompanyStatus.Rejected, company.Status);
        Assert.NotNull(company.StatusReason);
    }

    /// <summary>
    /// A Empresa aprovada altera os dados de registo com o mesmo NIF e mantém-se aprovada (RF049).
    /// </summary>
    [Fact]
    public void ApprovedCompanyUpdatesTheRegistrationKeepingTheTaxId()
    {
        Company company = CompanyIn(CompanyStatus.Approved);
        Guid otherLocation = Guid.NewGuid();

        company.UpdateRegistration(ValidData() with
        {
            Address = "Rua Nova de Santa Cruz, 15, 4710-409 Braga",
            LocationId = otherLocation,
            ContactEmail = "recrutamento@lumen-exemplo.pt",
            ContactPhone = "253000222",
            Industry = Industry.Services,
        });

        Assert.Equal(CompanyStatus.Approved, company.Status);
        Assert.Equal("Rua Nova de Santa Cruz, 15, 4710-409 Braga", company.Address);
        Assert.Equal(otherLocation, company.LocationId);
        Assert.Equal("recrutamento@lumen-exemplo.pt", company.ContactEmail);
        Assert.Equal("253000222", company.ContactPhone);
        Assert.Equal(Industry.Services, company.Industry);
        Assert.Equal(Submitted, company.SubmittedAt);
    }

    /// <summary>
    /// O NIF não pode ser alterado depois da aprovação (RF049).
    /// </summary>
    [Fact]
    public void TaxIdCannotChangeAfterApproval()
    {
        Company company = CompanyIn(CompanyStatus.Approved);

        Assert.Throws<ArgumentException>(() => company.UpdateRegistration(ValidData() with { TaxId = "509999999", Address = "Outra morada" }));
        Assert.Equal("509123456", company.TaxId);
        Assert.Equal("Avenida da Liberdade, 100, 4710-000 Braga", company.Address);
    }

    /// <summary>
    /// Só a Empresa aprovada altera os dados de registo, a página, o logótipo e a galeria (política
    /// <c>ApprovedCompany</c>, documentação da API v02, secção 6.4).
    /// </summary>
    /// <param name="status">Estado que não é aprovado.</param>
    [Theory]
    [InlineData(CompanyStatus.Pending)]
    [InlineData(CompanyStatus.Rejected)]
    [InlineData(CompanyStatus.Suspended)]
    public void ApprovedOnlyOperationsRequireApproved(CompanyStatus status)
    {
        Company company = CompanyIn(status);

        Assert.Throws<InvalidOperationException>(() => company.UpdateRegistration(ValidData()));
        Assert.Throws<InvalidOperationException>(() => company.UpdatePage("Descrição", "https://lumen.pt"));
        Assert.Throws<InvalidOperationException>(() => company.SetLogo("company-logos/logo.png"));
        Assert.Throws<InvalidOperationException>(() => company.AddGalleryPhoto("company-photos/foto.jpg"));
        Assert.Null(company.Description);
        Assert.Null(company.LogoPath);
        Assert.Empty(company.PhotoPaths);
    }

    /// <summary>
    /// A página guarda a descrição e o sítio na Internet; um valor nulo ou vazio limpa o campo (RF046).
    /// </summary>
    [Fact]
    public void UpdatePageSetsAndClearsDescriptionAndWebsite()
    {
        Company company = CompanyIn(CompanyStatus.Approved);

        company.UpdatePage("Software de gestão de frotas.", "HTTPS://www.lumen-exemplo.pt");
        Assert.Equal("Software de gestão de frotas.", company.Description);
        Assert.Equal("HTTPS://www.lumen-exemplo.pt", company.Website);

        company.UpdatePage(null, "  ");
        Assert.Null(company.Description);
        Assert.Null(company.Website);
    }

    /// <summary>
    /// A descrição tem no máximo 1000 caracteres (P08) e o sítio começa por <c>http://</c> ou <c>https://</c> (P10).
    /// </summary>
    [Fact]
    public void InvalidPageDataIsRejected()
    {
        Company company = CompanyIn(CompanyStatus.Approved);

        company.UpdatePage(new string('d', 1000), "http://" + new string('w', 493));
        Assert.Equal(1000, company.Description!.Length);
        Assert.Equal(500, company.Website!.Length);

        Assert.Throws<ArgumentException>(() => company.UpdatePage(new string('d', 1001), null));
        Assert.Throws<ArgumentException>(() => company.UpdatePage(null, "www.lumen.pt"));
        Assert.Throws<ArgumentException>(() => company.UpdatePage(null, "ftp://lumen.pt"));
        Assert.Throws<ArgumentException>(() => company.UpdatePage(null, "https://" + new string('w', 493)));
        Assert.Equal(1000, company.Description!.Length);
    }

    /// <summary>
    /// O logótipo substitui o anterior (RF047).
    /// </summary>
    [Fact]
    public void SetLogoReplacesThePreviousOne()
    {
        Company company = CompanyIn(CompanyStatus.Approved);

        company.SetLogo("company-logos/a.png");
        company.SetLogo("company-logos/b.png");

        Assert.Equal("company-logos/b.png", company.LogoPath);
        Assert.Throws<ArgumentException>(() => company.SetLogo(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => company.SetLogo(new string('p', 501)));
    }

    /// <summary>
    /// A galeria aceita até 6 fotografias, pela ordem em que são acrescentadas (RF048, P13).
    /// </summary>
    [Fact]
    public void GalleryAcceptsUpToSixPhotosInOrder()
    {
        Company company = CompanyIn(CompanyStatus.Approved);

        for (int index = 1; index <= Company.MaxGalleryPhotos; index++)
        {
            company.AddGalleryPhoto($"company-photos/{index}.jpg");
        }

        Assert.Equal(
            ["company-photos/1.jpg", "company-photos/2.jpg", "company-photos/3.jpg", "company-photos/4.jpg", "company-photos/5.jpg", "company-photos/6.jpg"],
            company.PhotoPaths);
        Assert.Throws<InvalidOperationException>(() => company.AddGalleryPhoto("company-photos/7.jpg"));
        Assert.Equal(6, company.PhotoPaths.Count);
    }

    /// <summary>
    /// O caminho da fotografia é obrigatório e tem no máximo 500 caracteres.
    /// </summary>
    [Fact]
    public void InvalidGalleryPathIsRejected()
    {
        Company company = CompanyIn(CompanyStatus.Approved);

        Assert.Throws<ArgumentException>(() => company.AddGalleryPhoto(string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => company.AddGalleryPhoto(new string('p', 501)));
        Assert.Empty(company.PhotoPaths);
    }

    private static CompanyRegistration ValidData()
    {
        return new CompanyRegistration
        {
            CompanyName = "Lumen Software, Lda.",
            TaxId = "509123456",
            Industry = Industry.Technology,
            Address = "Avenida da Liberdade, 100, 4710-000 Braga",
            LocationId = LocationId,
            ContactEmail = "geral@lumen-exemplo.pt",
            ContactPhone = "253000111",
            ResponsibleName = "Rui Matos",
        };
    }

    private static Company NewCompany()
    {
        return new Company(RecruiterId, ValidData(), Submitted);
    }

    private static Company CompanyIn(CompanyStatus status)
    {
        Company company = NewCompany();

        switch (status)
        {
            case CompanyStatus.Approved:
                company.Approve();
                break;
            case CompanyStatus.Rejected:
                company.Reject("A morada não corresponde à certidão.");
                break;
            case CompanyStatus.Suspended:
                company.Approve();
                company.Suspend();
                break;
        }

        return company;
    }
}
