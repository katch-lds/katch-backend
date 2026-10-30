// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.Extensions.Options;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes do <see cref="FileAccessService"/> (documentação da API v02, secção 6.9.1; RNF007, RF061), sobre uma pasta
/// temporária por teste que é apagada no fim.
/// </summary>
public sealed class FileAccessServiceTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];

    private readonly string root = Path.Combine(Path.GetTempPath(), "katch-unit-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage storage;
    private readonly FakeCandidates candidates = new();
    private readonly FakeMatches matches = new();
    private readonly FileAccessService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAccessServiceTests"/> class.
    /// </summary>
    public FileAccessServiceTests()
    {
        this.storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = this.root }));
        this.service = new FileAccessService(this.storage, this.candidates, this.matches);
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
    /// O logótipo, a fotografia da Empresa e a fotografia da vaga são obtidos por qualquer tipo de conta.
    /// </summary>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <param name="userType">Tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(FileKind.CompanyLogo, UserType.Candidate)]
    [InlineData(FileKind.CompanyLogo, UserType.Recruiter)]
    [InlineData(FileKind.CompanyLogo, UserType.Admin)]
    [InlineData(FileKind.CompanyPhoto, UserType.Candidate)]
    [InlineData(FileKind.CompanyPhoto, UserType.Recruiter)]
    [InlineData(FileKind.CompanyPhoto, UserType.Admin)]
    [InlineData(FileKind.JobPhoto, UserType.Candidate)]
    [InlineData(FileKind.JobPhoto, UserType.Recruiter)]
    [InlineData(FileKind.JobPhoto, UserType.Admin)]
    public async Task PublicKindsAreGotByAnyAccount(FileKind kind, UserType userType)
    {
        string name = await this.StoreAsync(kind, Png);

        FileContent file = await this.service.GetAsync(Guid.NewGuid(), userType, kind, name, TestContext.Current.CancellationToken);

        await using (file.Content)
        {
            Assert.Equal("image/png", file.ContentType);
        }
    }

    /// <summary>
    /// A fotografia de perfil é obtida pelo próprio Candidato e pelo Recrutador com um interesse na sua Empresa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ProfilePhotoIsGotByTheOwnerAndByTheRecruiterWithAnInterest()
    {
        Guid owner = Guid.NewGuid();
        Guid recruiter = Guid.NewGuid();
        string name = await this.StoreAsync(FileKind.ProfilePhoto, Png);
        this.candidates.Owner = (owner, $"profile_photo/{name}");
        this.matches.InterestedRecruiters.Add((owner, recruiter));

        FileContent byOwner = await this.service.GetAsync(owner, UserType.Candidate, FileKind.ProfilePhoto, name, TestContext.Current.CancellationToken);
        FileContent byRecruiter = await this.service.GetAsync(recruiter, UserType.Recruiter, FileKind.ProfilePhoto, name, TestContext.Current.CancellationToken);

        await byOwner.Content.DisposeAsync();
        await byRecruiter.Content.DisposeAsync();
        Assert.Equal("image/png", byOwner.ContentType);
    }

    /// <summary>
    /// A fotografia de perfil é recusada a outro Candidato, ao Recrutador sem interesse e ao Administrador.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Recruiter)]
    [InlineData(UserType.Admin)]
    public async Task ProfilePhotoIsRefusedToTheOthers(UserType userType)
    {
        string name = await this.StoreAsync(FileKind.ProfilePhoto, Png);
        this.candidates.Owner = (Guid.NewGuid(), $"profile_photo/{name}");

        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetAsync(Guid.NewGuid(), userType, FileKind.ProfilePhoto, name, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A fotografia que nenhum Candidato tem guardada não é obtida, ainda que o ficheiro exista.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ProfilePhotoOfNobodyIsRefused()
    {
        string name = await this.StoreAsync(FileKind.ProfilePhoto, Png);

        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetAsync(Guid.NewGuid(), UserType.Candidate, FileKind.ProfilePhoto, name, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// O curriculum vitae nunca é obtido, nem pelo dono.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CurriculumVitaeIsNeverGot()
    {
        Guid owner = Guid.NewGuid();
        string name = await this.StoreAsync(FileKind.CurriculumVitae, Pdf);
        this.candidates.Owner = (owner, $"curriculum_vitae/{name}");

        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetAsync(owner, UserType.Candidate, FileKind.CurriculumVitae, name, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Nomes inexistentes, de outro tipo ou com percursos de diretório dão todos o mesmo erro.
    /// </summary>
    /// <param name="fileName">Nome pedido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("00000000000000000000000000000000.png")]
    [InlineData("../company_logo/00000000000000000000000000000000.png")]
    [InlineData("..\\00000000000000000000000000000000.png")]
    [InlineData("a/b.png")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("..")]
    [InlineData("nome-escolhido.png")]
    [InlineData("")]
    public async Task InvalidOrMissingNamesAreRefused(string fileName)
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetAsync(Guid.NewGuid(), UserType.Admin, FileKind.CompanyLogo, fileName, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Um ficheiro de outro tipo, mesmo existindo, não é obtido pelo tipo pedido.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task FileOfAnotherKindIsRefused()
    {
        string name = await this.StoreAsync(FileKind.CompanyLogo, Png);

        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetAsync(Guid.NewGuid(), UserType.Admin, FileKind.JobPhoto, name, TestContext.Current.CancellationToken));
    }

    private async Task<string> StoreAsync(FileKind kind, byte[] content)
    {
        string path = await this.storage.SaveAsync(new MemoryStream(content), kind, TestContext.Current.CancellationToken);
        return path[(path.IndexOf('/', StringComparison.Ordinal) + 1)..];
    }

    private sealed class FakeCandidates : ICandidateRepository
    {
        public (Guid Id, string Path)? Owner { get; set; }

        public Task<Candidate?> GetByProfilePhotoPathAsync(string path, CancellationToken cancellationToken = default)
        {
            if (this.Owner is not { } owner || owner.Path != path)
            {
                return Task.FromResult<Candidate?>(null);
            }

            var user = new AppUser($"{owner.Id:N}@katch.pt", "hash", UserType.Candidate, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            var candidate = new Candidate(user, "Ana Silva", "912345678", Guid.NewGuid());
            typeof(Candidate).GetProperty(nameof(Candidate.Id))!.SetValue(candidate, owner.Id);
            return Task.FromResult<Candidate?>(candidate);
        }

        public Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Candidate?> GetWithProfileAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Candidate>> ListQuotaDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeMatches : IMatchRepository
    {
        public List<(Guid CandidateId, Guid RecruiterId)> InterestedRecruiters { get; } = [];

        public Task<bool> HasInterestFromCandidateInCompanyAsync(Guid candidateId, Guid recruiterId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.InterestedRecruiters.Contains((candidateId, recruiterId)));
        }

        public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Match match, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<WaitingInterest>> ListWaitingByJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
