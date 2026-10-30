// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes do <see cref="CandidateProfileService"/> nas operações da consulta, do perfil, das preferências, das
/// hiperligações, da fotografia e do curriculum vitae (documentação da API v02, secções 6.2.1 a 6.2.3 e 6.2.6 a 6.2.9),
/// com repositórios e armazenamento em memória.
/// </summary>
public sealed class CandidateProfileUpdateServiceTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];

    private readonly Location location = new("Guimarães", "Braga", 41.445m, -8.292m);
    private readonly Candidate candidate;
    private readonly Store store;
    private readonly CandidateProfileService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateProfileUpdateServiceTests"/> class.
    /// </summary>
    public CandidateProfileUpdateServiceTests()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = new AppUser("ana@exemplo.pt", "hash", UserType.Candidate, now, now);
        this.candidate = new Candidate(user, "Ana Ferreira", "912345678", this.location.Id);
        this.store = new Store(this.candidate, this.location);
        this.service = new CandidateProfileService(
            this.store,
            this.store,
            this.store,
            this.store,
            new FileValidator(),
            new FileUrlBuilder());
    }

    /// <summary>
    /// A consulta devolve a localidade com o distrito, o URL da fotografia e <c>hasCv</c>, nunca os caminhos.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetReturnsTheProfileWithUrlsAndNoPaths()
    {
        this.candidate.ProfilePhotoPath = "profile_photo/abc.png";
        this.candidate.CvPath = "curriculum_vitae/abc.pdf";

        CandidateProfileDto profile = await this.service.GetAsync(this.candidate.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Guimarães (Braga)", profile.LocationName);
        Assert.Equal("/api/files/PROFILE_PHOTO/abc.png", profile.PhotoUrl);
        Assert.True(profile.HasCv);
        Assert.Empty(profile.Links);
    }

    /// <summary>
    /// Um Candidato inexistente dá o ERR-20.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownCandidateIsNotFound()
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Os quatro campos são substituídos, com os espaços das extremidades retirados, e <c>null</c> limpa os opcionais.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateReplacesTheFourFieldsAndNullClears()
    {
        await this.service.UpdateAsync(
            this.candidate.Id,
            new UpdateCandidateProfileRequest("  Programadora ", this.location.Id, Availability.OneMonth, " Resumo "),
            TestContext.Current.CancellationToken);
        Assert.Equal("Programadora", this.candidate.DesiredRole);
        Assert.Equal("Resumo", this.candidate.ExperienceSummary);

        await this.service.UpdateAsync(
            this.candidate.Id,
            new UpdateCandidateProfileRequest("   ", this.location.Id, null, null),
            TestContext.Current.CancellationToken);

        Assert.Null(this.candidate.DesiredRole);
        Assert.Null(this.candidate.Availability);
        Assert.Null(this.candidate.ExperienceSummary);
    }

    /// <summary>
    /// 150 e 1000 caracteres são aceites e 151 e 1001 são recusados com o motivo do campo.
    /// </summary>
    /// <param name="roleLength">Comprimento da função.</param>
    /// <param name="summaryLength">Comprimento do resumo.</param>
    /// <param name="field">Campo em erro, ou vazio.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(150, 1000, "")]
    [InlineData(151, 1000, "desiredRole")]
    [InlineData(150, 1001, "experienceSummary")]
    public async Task UpdateTextLimits(int roleLength, int summaryLength, string field)
    {
        var request = new UpdateCandidateProfileRequest(new string('a', roleLength), this.location.Id, null, new string('b', summaryLength));

        if (field.Length == 0)
        {
            await this.service.UpdateAsync(this.candidate.Id, request, TestContext.Current.CancellationToken);
            return;
        }

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.UpdateAsync(this.candidate.Id, request, TestContext.Current.CancellationToken));
        Assert.Equal(field, Assert.Single(exception.Fields).Field);
    }

    /// <summary>
    /// Todos os erros do perfil são indicados juntos, pela ordem do corpo, e nada é gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateReportsAllErrorsTogetherAndSavesNothing()
    {
        var request = new UpdateCandidateProfileRequest(new string('a', 151), Guid.NewGuid(), (Availability)99, new string('b', 1001));

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.UpdateAsync(this.candidate.Id, request, TestContext.Current.CancellationToken));

        Assert.Equal(
            ["desiredRole", "locationId", "availability", "experienceSummary"],
            exception.Fields.Select(item => item.Field));
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// A localidade em falta é obrigatória e uma localidade desconhecida não pertence à lista.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateLocationIsRequiredAndMustBeInTheList()
    {
        ValidationFailedException missing = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.UpdateAsync(this.candidate.Id, UpdateCandidateProfileRequest.Empty, TestContext.Current.CancellationToken));
        ValidationFailedException unknown = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.UpdateAsync(this.candidate.Id, new UpdateCandidateProfileRequest(null, Guid.NewGuid(), null, null), TestContext.Current.CancellationToken));

        Assert.Equal("O campo é obrigatório.", Assert.Single(missing.Fields).Reason);
        Assert.Equal("O valor não pertence à lista pré-definida.", Assert.Single(unknown.Fields).Reason);
    }

    /// <summary>
    /// As preferências válidas são gravadas; <c>null</c> e as listas vazias significam sem filtro.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PreferencesAreSavedAndNullOrEmptyMeanNoFilter()
    {
        SearchPreferencesDto saved = await this.service.UpdatePreferencesAsync(
            this.candidate.Id,
            new SearchPreferencesDto(500, 1400.456m, [WorkMode.Remote, WorkMode.Remote], [ContractType.Permanent]),
            TestContext.Current.CancellationToken);

        Assert.Equal(500, saved.MaxDistanceKm);
        Assert.Equal(1400.46m, saved.MinSalaryExpectation);
        Assert.Equal([WorkMode.Remote], saved.WorkModes);

        SearchPreferencesDto cleared = await this.service.UpdatePreferencesAsync(
            this.candidate.Id,
            new SearchPreferencesDto(null, null, [], []),
            TestContext.Current.CancellationToken);

        Assert.Null(cleared.MaxDistanceKm);
        Assert.Null(cleared.MinSalaryExpectation);
        Assert.Empty(this.candidate.PreferredWorkModes);
        Assert.Empty(this.candidate.PreferredContractTypes);
    }

    /// <summary>
    /// A distância e a pretensão salarial fora dos limites são recusadas e as preferências anteriores mantêm-se; 1, 500 e
    /// o salário 0 são aceites.
    /// </summary>
    /// <param name="distance">Distância pedida.</param>
    /// <param name="salary">Pretensão salarial pedida.</param>
    /// <param name="field">Campo em erro, ou vazio.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(1, 0, "")]
    [InlineData(500, 0, "")]
    [InlineData(0, 100, "maxDistanceKm")]
    [InlineData(501, 100, "maxDistanceKm")]
    [InlineData(10, -0.01, "minSalaryExpectation")]
    public async Task PreferencesLimits(int distance, double salary, string field)
    {
        this.candidate.MaxDistanceKm = 30;
        var request = new SearchPreferencesDto(distance, (decimal)salary, [], []);

        if (field.Length == 0)
        {
            await this.service.UpdatePreferencesAsync(this.candidate.Id, request, TestContext.Current.CancellationToken);
            Assert.Equal(distance, this.candidate.MaxDistanceKm);
            return;
        }

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.UpdatePreferencesAsync(this.candidate.Id, request, TestContext.Current.CancellationToken));
        Assert.Equal(field, Assert.Single(exception.Fields).Field);
        Assert.Equal(30, this.candidate.MaxDistanceKm);
    }

    /// <summary>
    /// Os valores das listas fora das enumerações são recusados com o índice.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PreferencesRejectValuesOutsideTheEnumerations()
    {
        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.UpdatePreferencesAsync(
                this.candidate.Id,
                new SearchPreferencesDto(null, null, [WorkMode.Remote, (WorkMode)99], [(ContractType)99]),
                TestContext.Current.CancellationToken));

        Assert.Equal(["workModes[1]", "contractTypes[0]"], exception.Fields.Select(item => item.Field));
    }

    /// <summary>
    /// De 0 a 3 hiperligações são guardadas tal como indicadas e pela ordem; a lista nova substitui a anterior.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LinksAreSavedAsGivenInOrderAndReplaceThePreviousOnes()
    {
        CandidateProfileDto three = await this.service.SetLinksAsync(
            this.candidate.Id,
            new SetLinksRequest(["HTTPS://A.pt/X", " http://b.pt ", "https://c.pt"]),
            TestContext.Current.CancellationToken);
        Assert.Equal(["HTTPS://A.pt/X", "http://b.pt", "https://c.pt"], three.Links);

        CandidateProfileDto replaced = await this.service.SetLinksAsync(
            this.candidate.Id,
            new SetLinksRequest(["https://c.pt"]),
            TestContext.Current.CancellationToken);
        Assert.Equal(["https://c.pt"], replaced.Links);

        CandidateProfileDto none = await this.service.SetLinksAsync(this.candidate.Id, new SetLinksRequest([]), TestContext.Current.CancellationToken);
        Assert.Empty(none.Links);
    }

    /// <summary>
    /// Os erros das hiperligações são indicados juntos: mais de 3, sem esquema, acima de 500 e repetida.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LinksReportAllErrorsTogether()
    {
        string tooLong = "https://" + new string('a', 493);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.SetLinksAsync(
                this.candidate.Id,
                new SetLinksRequest(["ftp://x.pt", "https://ok.pt", "https://ok.pt", tooLong]),
                TestContext.Current.CancellationToken));

        Assert.Equal(["urls", "urls[0]", "urls[2]", "urls[3]"], exception.Fields.Select(item => item.Field));
        Assert.Equal("O endereço está repetido.", exception.Fields[2].Reason);
    }

    /// <summary>
    /// Os limites de 500 e 501 caracteres do endereço.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LinkLengthLimit()
    {
        await this.service.SetLinksAsync(
            this.candidate.Id,
            new SetLinksRequest(["https://" + new string('a', 492)]),
            TestContext.Current.CancellationToken);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.SetLinksAsync(
                this.candidate.Id,
                new SetLinksRequest(["https://" + new string('a', 493)]),
                TestContext.Current.CancellationToken));
        Assert.Equal("urls[0]", Assert.Single(exception.Fields).Field);
    }

    /// <summary>
    /// A fotografia é guardada, o caminho fica no Candidato, o URL é devolvido e o ficheiro anterior é eliminado depois
    /// de gravar.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PhotoIsSavedAndThePreviousFileIsDeletedAfterSaving()
    {
        this.candidate.ProfilePhotoPath = "profile_photo/antiga.png";
        this.store.Existing.Add("profile_photo/antiga.png");

        string url = await this.service.UploadPhotoAsync(this.candidate.Id, File("minha.png", Png), TestContext.Current.CancellationToken);

        Assert.StartsWith("/api/files/PROFILE_PHOTO/", url, StringComparison.Ordinal);
        Assert.DoesNotContain("minha", url, StringComparison.Ordinal);
        Assert.Equal(this.store.Saved.Single(), this.candidate.ProfilePhotoPath);
        Assert.Equal(["profile_photo/antiga.png"], this.store.Deleted);
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// Se a gravação falhar, o ficheiro novo é eliminado, o anterior mantém-se e o erro sobe (ERR-41 no concorrente).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task FailedSaveDeletesTheNewFileAndKeepsThePreviousOne()
    {
        this.candidate.CvPath = "curriculum_vitae/antigo.pdf";
        this.store.Existing.Add("curriculum_vitae/antigo.pdf");
        this.store.SaveFailure = new ConcurrencyConflictException(new InvalidOperationException("xmin"));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => this.service.UploadCvAsync(this.candidate.Id, File("cv.pdf", Pdf), TestContext.Current.CancellationToken));

        string created = Assert.Single(this.store.Saved);
        Assert.Equal([created], this.store.Deleted);
        Assert.DoesNotContain(created, this.store.Existing);
        Assert.Contains("curriculum_vitae/antigo.pdf", this.store.Existing);
    }

    /// <summary>
    /// Um ficheiro recusado pelo validador não é guardado e dá o ERR-11 com o motivo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectedFileIsNotStored()
    {
        FileRejectedException exception = await Assert.ThrowsAsync<FileRejectedException>(
            () => this.service.UploadPhotoAsync(this.candidate.Id, File("foto.png", Pdf), TestContext.Current.CancellationToken));

        Assert.Equal("O ficheiro tem de estar no formato JPEG ou PNG.", exception.Reason);
        Assert.Empty(this.store.Saved);
    }

    /// <summary>
    /// Sem curriculum vitae a obtenção dá o ERR-20 e com ele devolve o PDF.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetCvWithoutCvIsNotFoundAndWithCvReturnsThePdf()
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetCvAsync(this.candidate.Id, TestContext.Current.CancellationToken));

        await this.service.UploadCvAsync(this.candidate.Id, File("cv.pdf", Pdf), TestContext.Current.CancellationToken);
        FileContent cv = await this.service.GetCvAsync(this.candidate.Id, TestContext.Current.CancellationToken);

        await cv.Content.DisposeAsync();
        Assert.Equal("application/pdf", cv.ContentType);
    }

    /// <summary>
    /// O conflito de concorrência na gravação do perfil sobe como <see cref="ConcurrencyConflictException"/>, que o
    /// tratador global converte em ERR-41.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrencyConflictOnSaveRises()
    {
        this.store.SaveFailure = new ConcurrencyConflictException(new InvalidOperationException("xmin"));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => this.service.UpdateAsync(
                this.candidate.Id,
                new UpdateCandidateProfileRequest(null, this.location.Id, null, null),
                TestContext.Current.CancellationToken));
    }

    private static FormFile File(string fileName, byte[] content)
    {
        return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName);
    }

    private sealed class Store : ICandidateRepository, IReferenceDataRepository, IUnitOfWork, IFileStorage
    {
        private readonly Candidate candidate;
        private readonly Location location;

        public Store(Candidate candidate, Location location)
        {
            this.candidate = candidate;
            this.location = location;
        }

        public Exception? SaveFailure { get; set; }

        public int Saves { get; private set; }

        public List<string> Saved { get; } = [];

        public List<string> Deleted { get; } = [];

        public HashSet<string> Existing { get; } = [];

        public Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Candidate?>(id == this.candidate.Id ? this.candidate : null);
        }

        public Task<Candidate?> GetWithProfileAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return this.GetByIdAsync(id, cancellationToken);
        }

        public Task<Location?> GetLocationAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Location?>(id == this.location.Id ? this.location : null);
        }

        public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Benefit?> GetBenefitAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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

        public Task<string> SaveAsync(Stream content, FileKind kind, CancellationToken cancellationToken = default)
        {
            string path = $"{FileRules.Folder(kind)}/{Guid.NewGuid():N}.bin";
            this.Saved.Add(path);
            this.Existing.Add(path);
            return Task.FromResult(path);
        }

        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
        {
            return this.Existing.Contains(path)
                ? Task.FromResult<Stream>(new MemoryStream([1, 2, 3]))
                : throw new FileNotFoundException();
        }

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            this.Deleted.Add(path);
            this.Existing.Remove(path);
            return Task.CompletedTask;
        }

        public Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Candidate>> ListQuotaDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Candidate?> GetByProfilePhotoPathAsync(string path, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void DiscardChanges()
        {
            throw new NotSupportedException();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
