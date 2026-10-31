// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes das operações do Recrutador sobre a própria Empresa em <see cref="CompanyService"/>: dados de registo, página
/// de apresentação, logótipo e galeria (documentação da API v02, secções 6.4.4 a 6.4.9; RF046 a RF049, RF112; P01, P08,
/// P10, P13), com repositórios e armazenamento em memória.
/// </summary>
public sealed class CompanyPageServiceTests
{
    private const string TaxId = "509123456";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] Gif = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid RecruiterId = Guid.Parse("8b1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e02");

    private readonly Location braga = new("Braga", "Braga", 41.545450m, -8.426507m);
    private readonly Store store;
    private readonly CompanyService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyPageServiceTests"/> class.
    /// </summary>
    public CompanyPageServiceTests()
    {
        this.store = new Store(this.braga);
        this.service = new CompanyService(
            this.store,
            this.store,
            this.store,
            this.store,
            new FixedClock(),
            this.store,
            new FileValidator(),
            new FileUrlBuilder());
    }

    /// <summary>
    /// Gets the states in which the recruiter reads the registration data of the company (6.4.8).
    /// </summary>
    public static TheoryData<CompanyStatus> AllStates => new()
    {
        CompanyStatus.Pending,
        CompanyStatus.Approved,
        CompanyStatus.Rejected,
        CompanyStatus.Suspended,
    };

    /// <summary>
    /// Os dados de registo são lidos em qualquer estado da Empresa, com a localidade no formato «Localidade (Distrito)»
    /// (6.4.8; RF044, RF045, RF049).
    /// </summary>
    /// <param name="status">Estado da Empresa.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(AllStates))]
    public async Task RegistrationIsReadInAnyState(CompanyStatus status)
    {
        Company company = this.AddCompany(status);

        CompanyRegistrationDto registration = await this.service.GetRegistrationAsync(
            RecruiterId,
            TestContext.Current.CancellationToken);

        Assert.Equal(company.Id, registration.CompanyId);
        Assert.Equal(TaxId, registration.TaxId);
        Assert.Equal(this.braga.Id, registration.LocationId);
        Assert.Equal("Braga (Braga)", registration.LocationName);
        Assert.Equal("253000111", registration.ContactPhone);
        Assert.Equal(Now, registration.SubmittedAt);
    }

    /// <summary>
    /// Sem Empresa registada, a consulta dos dados de registo e a da página dão o ERR-20.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReadsWithoutACompanyReturnErr20()
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetRegistrationAsync(RecruiterId, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetPageAsync(RecruiterId, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A alteração do contacto telefónico da Empresa aprovada é gravada, a Empresa mantém-se aprovada e fica registado
    /// <c>COMPANY_UPDATED</c> com o Recrutador como autor, na mesma gravação (RF049, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateOfTheApprovedCompanyKeepsItApprovedAndLogsTheChange()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);

        CompanyStatusDto status = await this.service.UpdateRegistrationAsync(
            RecruiterId,
            this.Request(contactPhone: "253000222", address: "  Rua Nova de Santa Cruz, 15  "),
            TestContext.Current.CancellationToken);

        Assert.Equal(CompanyStatus.Approved, status.Status);
        Assert.Null(status.RejectionReason);
        Assert.Equal(CompanyStatus.Approved, company.Status);
        Assert.Equal("253000222", company.ContactPhone);
        Assert.Equal("Rua Nova de Santa Cruz, 15", company.Address);
        Assert.Equal(TaxId, company.TaxId);
        Assert.Equal((RecruiterId, OperationType.CompanyUpdated, company.Id), Assert.Single(this.store.Logged));
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// A alteração do NIF depois da aprovação é rejeitada com o motivo do catálogo, o valor anterior mantém-se e nada é
    /// gravado nem registado (RF049).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ChangeOfTheTaxIdIsRejectedAndNothingIsSaved()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.UpdateRegistrationAsync(
                RecruiterId,
                this.Request(taxId: "509999999", contactPhone: "253000222"),
                TestContext.Current.CancellationToken));

        FieldErrorDto error = Assert.Single(exception.Fields);
        Assert.Equal("taxId", error.Field);
        Assert.Equal("O número de identificação fiscal não pode ser alterado depois da aprovação.", error.Reason);
        Assert.Equal(TaxId, company.TaxId);
        Assert.Equal("253000111", company.ContactPhone);
        Assert.Empty(this.store.Logged);
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// Um NIF mal formado é indicado pelo formato, e não como alteração; os restantes campos em erro vêm na mesma
    /// resposta (RF042, RF049).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InvalidFieldsOfTheUpdateAreReportedTogether()
    {
        this.AddCompany(CompanyStatus.Approved);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.UpdateRegistrationAsync(
                RecruiterId,
                this.Request(taxId: "5091", contactPhone: "25300", address: " "),
                TestContext.Current.CancellationToken));

        Assert.Equal(
            [
                ("taxId", FieldReasons.TaxIdFormat),
                ("address", FieldReasons.Required),
                ("contactPhone", FieldReasons.PhoneNumberFormat),
            ],
            exception.Fields.Select(field => (field.Field, field.Reason)));
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// O NIF da própria Empresa não é indicado como já registado (a unicidade só se verifica contra as outras).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OwnTaxIdIsNotReportedAsTaken()
    {
        this.AddCompany(CompanyStatus.Approved);
        this.store.TaxIdsOfOtherCompanies.Add(TaxId);

        CompanyStatusDto status = await this.service.UpdateRegistrationAsync(
            RecruiterId,
            this.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal(CompanyStatus.Approved, status.Status);
    }

    /// <summary>
    /// A página gravada devolve a descrição e o sítio na Internet sem os espaços nas extremidades (RF046).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PageIsSavedAndReturned()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);

        CompanyPageDto page = await this.service.UpdatePageAsync(
            RecruiterId,
            new CompanyPageRequest("  Software de gestão de frotas.  ", " https://www.lumen-exemplo.pt "),
            TestContext.Current.CancellationToken);

        Assert.Equal(company.Id, page.CompanyId);
        Assert.Equal("Lumen Software, Lda.", page.CompanyName);
        Assert.Equal("Software de gestão de frotas.", page.Description);
        Assert.Equal("https://www.lumen-exemplo.pt", page.Website);
        Assert.Null(page.LogoUrl);
        Assert.Empty(page.PhotoUrls);
        Assert.Equal("https://www.lumen-exemplo.pt", company.Website);
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// Um valor nulo, vazio ou só com espaços limpa o campo (6.4.5).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NullOrBlankValuesClearThePage()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);
        company.UpdatePage("Descrição anterior.", "http://anterior.pt");

        CompanyPageDto page = await this.service.UpdatePageAsync(
            RecruiterId,
            new CompanyPageRequest(null, "   "),
            TestContext.Current.CancellationToken);

        Assert.Null(page.Description);
        Assert.Null(page.Website);
        Assert.Null(company.Description);
        Assert.Null(company.Website);
    }

    /// <summary>
    /// A descrição acima de 1000 caracteres (P08) e o endereço sem <c>http://</c> nem <c>https://</c> (P10) são indicados
    /// na mesma resposta e nada é gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InvalidPageFieldsAreReportedTogether()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.UpdatePageAsync(
                RecruiterId,
                new CompanyPageRequest(new string('a', 1001), "www.lumen-exemplo.pt"),
                TestContext.Current.CancellationToken));

        Assert.Equal(
            [
                ("description", "O texto não pode exceder 1000 caracteres."),
                ("website", "O endereço tem de começar por http:// ou https://."),
            ],
            exception.Fields.Select(field => (field.Field, field.Reason)));
        Assert.Null(company.Description);
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// Os limites da página são inclusivos: 1000 caracteres na descrição e 500 no endereço são aceites; 501 no endereço
    /// é rejeitado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PageLimitsAreInclusive()
    {
        this.AddCompany(CompanyStatus.Approved);
        string website = "https://" + new string('a', 492);

        CompanyPageDto page = await this.service.UpdatePageAsync(
            RecruiterId,
            new CompanyPageRequest(new string('a', 1000), website),
            TestContext.Current.CancellationToken);

        Assert.Equal(500, page.Website!.Length);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.UpdatePageAsync(
                RecruiterId,
                new CompanyPageRequest(null, website + "a"),
                TestContext.Current.CancellationToken));

        Assert.Equal(("website", "O texto não pode exceder 500 caracteres."), (exception.Fields[0].Field, exception.Fields[0].Reason));
    }

    /// <summary>
    /// A página consultada traz os URL da API do logótipo e da galeria, pela ordem, e nunca os caminhos (6.4.9).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PageHasTheUrlsOfTheLogoAndTheGallery()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);
        company.SetLogo("company_logo/0123456789abcdef0123456789abcdef.png");
        company.AddGalleryPhoto("company_photo/11111111111111111111111111111111.jpg");
        company.AddGalleryPhoto("company_photo/22222222222222222222222222222222.png");

        CompanyPageDto page = await this.service.GetPageAsync(RecruiterId, TestContext.Current.CancellationToken);

        Assert.Equal("/api/files/COMPANY_LOGO/0123456789abcdef0123456789abcdef.png", page.LogoUrl);
        Assert.Equal(
            [
                "/api/files/COMPANY_PHOTO/11111111111111111111111111111111.jpg",
                "/api/files/COMPANY_PHOTO/22222222222222222222222222222222.png",
            ],
            page.PhotoUrls);
    }

    /// <summary>
    /// O logótipo é guardado com nome gerado pelo servidor, substitui o anterior, que é apagado depois da gravação, e o
    /// URL devolvido é o da API (RF047).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LogoIsStoredWithAGeneratedNameAndReplacesThePreviousOne()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);
        company.SetLogo("company_logo/antigo.png");

        string url = await this.service.UploadLogoAsync(
            RecruiterId,
            File("logotipo da empresa.png", Png),
            TestContext.Current.CancellationToken);

        string saved = Assert.Single(this.store.Saved);
        Assert.Equal(saved, company.LogoPath);
        Assert.Equal("/api/files/COMPANY_LOGO/" + saved["company_logo/".Length..], url);
        Assert.DoesNotContain("logotipo", url, StringComparison.Ordinal);
        Assert.Equal(["company_logo/antigo.png"], this.store.Deleted);
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// Um logótipo acima de 2 MB, um GIF ou a falta do ficheiro são rejeitados com o motivo e nada é guardado (RF047,
    /// P01, RNF016).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InvalidLogoIsRejectedWithTheReasonAndNotStored()
    {
        this.AddCompany(CompanyStatus.Approved);
        byte[] large = new byte[(2 * FileRules.Megabyte) + 1];
        Png.CopyTo(large, 0);

        FileRejectedException tooLarge = await Assert.ThrowsAsync<FileRejectedException>(
            () => this.service.UploadLogoAsync(RecruiterId, File("logo.png", large), TestContext.Current.CancellationToken));
        FileRejectedException gif = await Assert.ThrowsAsync<FileRejectedException>(
            () => this.service.UploadLogoAsync(RecruiterId, File("logo.gif", Gif), TestContext.Current.CancellationToken));
        FileRejectedException disguised = await Assert.ThrowsAsync<FileRejectedException>(
            () => this.service.UploadLogoAsync(RecruiterId, File("logo.png", Gif), TestContext.Current.CancellationToken));
        FileRejectedException missing = await Assert.ThrowsAsync<FileRejectedException>(
            () => this.service.UploadLogoAsync(RecruiterId, null, TestContext.Current.CancellationToken));

        Assert.Equal("O ficheiro excede 2 MB.", tooLarge.Reason);
        Assert.Equal("O ficheiro tem de estar no formato JPEG ou PNG.", gif.Reason);
        Assert.Equal("O ficheiro tem de estar no formato JPEG ou PNG.", disguised.Reason);
        Assert.Equal("O campo é obrigatório.", missing.Reason);
        Assert.Empty(this.store.Saved);
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// Se a gravação falhar, o logótipo novo é apagado, o anterior mantém-se e o erro sobe.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task FailedSaveDeletesTheNewLogoAndKeepsThePreviousOne()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);
        company.SetLogo("company_logo/antigo.png");
        this.store.SaveFailure = new InvalidOperationException("falha da gravação");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => this.service.UploadLogoAsync(RecruiterId, File("logo.png", Png), TestContext.Current.CancellationToken));

        string created = Assert.Single(this.store.Saved);
        Assert.Equal([created], this.store.Deleted);
    }

    /// <summary>
    /// As fotografias são acrescentadas no fim da galeria, com nome gerado pelo servidor, e a resposta traz o URL da API
    /// (RF048).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GalleryPhotosAreAppendedInOrder()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);

        string first = await this.service.AddGalleryPhotoAsync(
            RecruiterId,
            File("escritorio.jpg", Jpeg),
            TestContext.Current.CancellationToken);
        string second = await this.service.AddGalleryPhotoAsync(
            RecruiterId,
            File("equipa.png", Png),
            TestContext.Current.CancellationToken);

        Assert.Equal(this.store.Saved, company.PhotoPaths);
        Assert.StartsWith("/api/files/COMPANY_PHOTO/", first, StringComparison.Ordinal);
        Assert.EndsWith(".jpg", first, StringComparison.Ordinal);
        Assert.EndsWith(".png", second, StringComparison.Ordinal);
        Assert.DoesNotContain("escritorio", first, StringComparison.Ordinal);
        Assert.Empty(this.store.Deleted);
        Assert.Equal(2, this.store.Saves);
    }

    /// <summary>
    /// Com 6 fotografias na galeria, a sétima é rejeitada com o ERR-39 e não chega ao armazenamento (RF048, P13).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SeventhGalleryPhotoReturnsErr39AndIsNotStored()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);
        for (int index = 0; index < Company.MaxGalleryPhotos; index++)
        {
            await this.service.AddGalleryPhotoAsync(RecruiterId, File("foto.png", Png), TestContext.Current.CancellationToken);
        }

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.AddGalleryPhotoAsync(RecruiterId, File("foto.png", Png), TestContext.Current.CancellationToken));

        Assert.Equal("ERR-39", exception.Error.Code);
        Assert.Equal("Foi atingido o número máximo de fotografias (6).", exception.Error.Message);
        Assert.Equal(Company.MaxGalleryPhotos, company.PhotoPaths.Count);
        Assert.Equal(Company.MaxGalleryPhotos, this.store.Saved.Count);
        Assert.Equal(Company.MaxGalleryPhotos, this.store.Saves);
    }

    /// <summary>
    /// Uma fotografia da galeria acima de 5 MB é rejeitada com o motivo; uma de 5 MB exatos é aceite (P01).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GalleryPhotoAboveFiveMegabytesIsRejected()
    {
        Company company = this.AddCompany(CompanyStatus.Approved);
        byte[] limit = new byte[5 * FileRules.Megabyte];
        Jpeg.CopyTo(limit, 0);
        byte[] large = new byte[limit.Length + 1];
        Jpeg.CopyTo(large, 0);

        FileRejectedException exception = await Assert.ThrowsAsync<FileRejectedException>(
            () => this.service.AddGalleryPhotoAsync(RecruiterId, File("foto.jpg", large), TestContext.Current.CancellationToken));
        await this.service.AddGalleryPhotoAsync(RecruiterId, File("foto.jpeg", limit), TestContext.Current.CancellationToken);

        Assert.Equal("O ficheiro excede 5 MB.", exception.Reason);
        Assert.Single(company.PhotoPaths);
    }

    /// <summary>
    /// Se a gravação da fotografia falhar, o ficheiro novo é apagado e o erro sobe.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task FailedSaveDeletesTheNewGalleryPhoto()
    {
        this.AddCompany(CompanyStatus.Approved);
        this.store.SaveFailure = new InvalidOperationException("falha da gravação");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => this.service.AddGalleryPhotoAsync(RecruiterId, File("foto.png", Png), TestContext.Current.CancellationToken));

        string created = Assert.Single(this.store.Saved);
        Assert.Equal([created], this.store.Deleted);
    }

    private static FormFile File(string fileName, byte[] content)
    {
        return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName);
    }

    private Company AddCompany(CompanyStatus status)
    {
        var company = new Company(
            RecruiterId,
            new CompanyRegistration
            {
                CompanyName = "Lumen Software, Lda.",
                TaxId = TaxId,
                Industry = Industry.Technology,
                Address = "Avenida da Liberdade, 100, 4710-000 Braga",
                LocationId = this.braga.Id,
                ContactEmail = "geral@lumen-exemplo.pt",
                ContactPhone = "253000111",
                ResponsibleName = "Rui Matos",
            },
            Now);

        if (status is CompanyStatus.Approved or CompanyStatus.Suspended)
        {
            company.Approve();
        }

        if (status == CompanyStatus.Suspended)
        {
            company.Suspend();
        }

        if (status == CompanyStatus.Rejected)
        {
            company.Reject("A morada indicada não corresponde à certidão permanente.");
        }

        this.store.Company = company;
        return company;
    }

    private CompanyRegistrationRequest Request(
        string taxId = TaxId,
        string contactPhone = "253000111",
        string address = "Avenida da Liberdade, 100, 4710-000 Braga")
    {
        return new CompanyRegistrationRequest(
            "Lumen Software, Lda.",
            taxId,
            Industry.Technology,
            address,
            this.braga.Id,
            "geral@lumen-exemplo.pt",
            contactPhone,
            "Rui Matos");
    }

    private sealed class Store : ICompanyRepository, IReferenceDataRepository, IUnitOfWork, IFileStorage, IOperationLogger
    {
        private readonly Location location;

        public Store(Location location)
        {
            this.location = location;
        }

        public Company? Company { get; set; }

        public List<string> TaxIdsOfOtherCompanies { get; } = [];

        public List<string> Saved { get; } = [];

        public List<string> Deleted { get; } = [];

        public List<(Guid? UserId, OperationType Operation, Guid EntityId)> Logged { get; } = [];

        public int Saves { get; private set; }

        public Exception? SaveFailure { get; set; }

        public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Company?.RecruiterId == recruiterId ? this.Company : null);
        }

        public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.TaxIdsOfOtherCompanies.Contains(taxId));
        }

        public Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Company>> ListPendingAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == this.location.Id);
        }

        public Task<Location?> GetLocationAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == this.location.Id ? this.location : null);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (this.SaveFailure is not null)
            {
                throw this.SaveFailure;
            }

            this.Saves++;
            return Task.FromResult(1);
        }

        public void DiscardChanges()
        {
            throw new NotSupportedException();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public async Task<string> SaveAsync(Stream content, FileKind kind, CancellationToken cancellationToken = default)
        {
            byte[] header = new byte[FileRules.SignatureLength];
            int read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
            string extension = FileRules.Extension(FileRules.Detect(header.AsSpan(0, read)));
            string path = $"{FileRules.Folder(kind)}/{Guid.NewGuid():N}{extension}";
            this.Saved.Add(path);
            return path;
        }

        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            this.Deleted.Add(path);
            return Task.CompletedTask;
        }

        public Task LogAsync(Guid? userId, OperationType operation, Guid entityId, CancellationToken cancellationToken = default)
        {
            this.Logged.Add((userId, operation, entityId));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
