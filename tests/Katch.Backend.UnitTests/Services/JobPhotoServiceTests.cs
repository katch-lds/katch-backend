// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes de <see cref="JobPhotoService"/> (documentação da API v02, secção 6.5.9; RF052, RF059; RNF016; P01, P13), com
/// repositórios em memória e o armazenamento local numa pasta temporária de cada teste, apagada no fim.
/// </summary>
public sealed class JobPhotoServiceTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];

    private readonly string root = Path.Combine(Path.GetTempPath(), "katch-unit-" + Guid.NewGuid().ToString("N"));
    private readonly Store store = new();
    private readonly Guid recruiterId = Guid.NewGuid();
    private readonly Company company;
    private readonly JobPhotoService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobPhotoServiceTests"/> class.
    /// </summary>
    public JobPhotoServiceTests()
    {
        this.company = NewCompany(this.recruiterId);
        this.store.Companies.Add(this.company);
        this.service = new JobPhotoService(
            this.store,
            this.store,
            new FileValidator(),
            new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = this.root })),
            new FileUrlBuilder(),
            this.store);
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
    /// Uma fotografia válida fica no fim de <c>photo_paths</c>, com um nome gerado, e o serviço devolve o URL da API.
    /// </summary>
    /// <param name="kind">Formato enviado.</param>
    /// <param name="fileName">Nome enviado.</param>
    /// <param name="extension">Extensão esperada.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("jpeg", "equipa.jpg", ".jpg")]
    [InlineData("png", "equipa.png", ".png")]
    public async Task ValidPhotoIsSavedAndItsUrlReturned(string kind, string fileName, string extension)
    {
        Job job = this.NewJob();

        string url = await this.service.AddPhotoAsync(this.recruiterId, job.Id, File(fileName, Content(kind)), TestContext.Current.CancellationToken);

        string path = Assert.Single(job.PhotoPaths);
        Assert.Matches(@"^job_photo/[0-9a-f]{32}\.(jpg|png)$", path);
        Assert.EndsWith(extension, path, StringComparison.Ordinal);
        Assert.Equal($"/api/files/JOB_PHOTO/{path["job_photo/".Length..]}", url);
        Assert.True(System.IO.File.Exists(Path.Combine(this.root, path)));
        Assert.Equal(1, this.store.Saves);
    }

    /// <summary>
    /// As fotografias ficam pela ordem de carregamento, e cinco são aceites.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PhotosKeepTheUploadOrderUpToFive()
    {
        Job job = this.NewJob();
        var urls = new List<string>();

        for (int count = 0; count < Job.MaxPhotos; count++)
        {
            urls.Add(await this.service.AddPhotoAsync(this.recruiterId, job.Id, File("foto.png", Png), TestContext.Current.CancellationToken));
        }

        Assert.Equal(5, job.PhotoPaths.Count);
        Assert.Equal(urls, job.PhotoPaths.Select(path => $"/api/files/JOB_PHOTO/{path["job_photo/".Length..]}"));
        Assert.Equal(5, this.StoredFiles().Count);
    }

    /// <summary>
    /// A sexta fotografia dá o ERR-39 com «(5)» e o ficheiro não fica no armazenamento (P13).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SixthPhotoThrowsErr39AndLeavesNoFile()
    {
        Job job = this.NewJob(photos: 5);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.AddPhotoAsync(this.recruiterId, job.Id, File("foto.jpg", Jpeg), TestContext.Current.CancellationToken));

        Assert.Equal("ERR-39", exception.Error.Code);
        Assert.Equal("Foi atingido o número máximo de fotografias (5).", exception.Error.Message);
        Assert.Equal(5, job.PhotoPaths.Count);
        Assert.Empty(this.StoredFiles());
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// A vaga encerrada dá o ERR-30 com o estado atual, mesmo com o limite atingido ou com um ficheiro inválido
    /// (ordem: titularidade, estado, limite, ficheiro).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ClosedJobThrowsErr30BeforeTheOtherChecks()
    {
        Job job = this.NewJob(JobStatus.Closed, photos: 5);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.AddPhotoAsync(this.recruiterId, job.Id, File("foto.jpg", Pdf), TestContext.Current.CancellationToken));

        Assert.Equal("ERR-30", exception.Error.Code);
        Assert.Equal("A operação não é permitida no estado atual: encerrada.", exception.Error.Message);
        Assert.Empty(this.StoredFiles());
    }

    /// <summary>
    /// Com o limite atingido e um ficheiro inválido, o erro é o ERR-39 e não o ERR-11.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LimitIsCheckedBeforeTheFile()
    {
        Job job = this.NewJob(photos: 5);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.AddPhotoAsync(this.recruiterId, job.Id, File("foto.jpg", Pdf), TestContext.Current.CancellationToken));

        Assert.Equal("ERR-39", exception.Error.Code);
    }

    /// <summary>
    /// As vagas não publicadas, publicadas e suspensas aceitam fotografias.
    /// </summary>
    /// <param name="status">Estado da vaga.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(JobStatus.Unpublished)]
    [InlineData(JobStatus.Published)]
    [InlineData(JobStatus.Suspended)]
    public async Task OpenStatesAcceptPhotos(JobStatus status)
    {
        Job job = this.NewJob(status);

        await this.service.AddPhotoAsync(this.recruiterId, job.Id, File("foto.jpg", Jpeg), TestContext.Current.CancellationToken);

        Assert.Single(job.PhotoPaths);
    }

    /// <summary>
    /// Uma vaga inexistente ou eliminada dá o ERR-20, e uma vaga de outra Empresa o ERR-08; nada é gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MissingDeletedAndForeignJobsAreRejectedWithoutSavingAFile()
    {
        Job deleted = this.NewJob();
        deleted.MarkDeleted(DateTimeOffset.UtcNow);
        Job foreign = NewJobFor(Guid.NewGuid());
        this.store.Jobs.Add(foreign);
        CancellationToken token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.AddPhotoAsync(this.recruiterId, Guid.NewGuid(), File("f.jpg", Jpeg), token));
        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.AddPhotoAsync(this.recruiterId, deleted.Id, File("f.jpg", Jpeg), token));
        await Assert.ThrowsAsync<ElementForbiddenException>(() => this.service.AddPhotoAsync(this.recruiterId, foreign.Id, File("f.jpg", Jpeg), token));

        Assert.Empty(this.StoredFiles());
        Assert.Empty(deleted.PhotoPaths);
        Assert.Empty(foreign.PhotoPaths);
    }

    /// <summary>
    /// Ficheiros em falta, vazios ou que não são JPEG nem PNG verdadeiros dão o ERR-11 e nada é gravado (RNF016).
    /// </summary>
    /// <param name="fileName">Nome enviado.</param>
    /// <param name="kind">Conteúdo enviado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("foto.jpg", "text")]
    [InlineData("foto.png", "pdf")]
    [InlineData("foto.gif", "gif")]
    [InlineData("foto.jpg", "gif")]
    [InlineData("foto.jpg", "png")]
    [InlineData("foto.png", "jpeg")]
    [InlineData("foto", "png")]
    [InlineData("foto.jpg", "empty")]
    public async Task InvalidFilesThrowErr11AndSaveNothing(string fileName, string kind)
    {
        Job job = this.NewJob();

        FileRejectedException exception = await Assert.ThrowsAsync<FileRejectedException>(
            () => this.service.AddPhotoAsync(this.recruiterId, job.Id, File(fileName, Content(kind)), TestContext.Current.CancellationToken));

        Assert.False(string.IsNullOrWhiteSpace(exception.Reason));
        Assert.Empty(job.PhotoPaths);
        Assert.Empty(this.StoredFiles());
        Assert.Equal(0, this.store.Saves);
    }

    /// <summary>
    /// Sem ficheiro, o pedido dá o ERR-11.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MissingFileThrowsErr11()
    {
        Job job = this.NewJob();

        await Assert.ThrowsAsync<FileRejectedException>(
            () => this.service.AddPhotoAsync(this.recruiterId, job.Id, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 5 MB exatos são aceites; mais de 5 MB dão o ERR-11 com o motivo da dimensão (P01).
    /// </summary>
    /// <param name="length">Dimensão do ficheiro.</param>
    /// <param name="accepted">Se é aceite.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(5 * 1024 * 1024, true)]
    [InlineData((5 * 1024 * 1024) + 1, false)]
    public async Task SizeLimitIs5Mb(int length, bool accepted)
    {
        Job job = this.NewJob();
        byte[] content = new byte[length];
        Jpeg.CopyTo(content, 0);
        CancellationToken token = TestContext.Current.CancellationToken;

        if (accepted)
        {
            await this.service.AddPhotoAsync(this.recruiterId, job.Id, File("foto.jpg", content), token);
            Assert.Single(job.PhotoPaths);
        }
        else
        {
            FileRejectedException exception = await Assert.ThrowsAsync<FileRejectedException>(
                () => this.service.AddPhotoAsync(this.recruiterId, job.Id, File("foto.jpg", content), token));
            Assert.Equal("O ficheiro excede 5 MB.", exception.Reason);
            Assert.Empty(job.PhotoPaths);
        }
    }

    /// <summary>
    /// O nome enviado pelo cliente nunca entra no caminho guardado.
    /// </summary>
    /// <param name="clientName">Nome enviado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(@"..\..\x.jpg")]
    [InlineData("../../x.jpg")]
    [InlineData("x.jpg")]
    public async Task ClientFileNameNeverEntersThePath(string clientName)
    {
        Job job = this.NewJob();

        string url = await this.service.AddPhotoAsync(this.recruiterId, job.Id, File(clientName, Jpeg), TestContext.Current.CancellationToken);

        string path = Assert.Single(job.PhotoPaths);
        Assert.Matches(@"^job_photo/[0-9a-f]{32}\.jpg$", path);
        Assert.DoesNotContain("x.jpg", path, StringComparison.Ordinal);
        Assert.DoesNotContain("x.jpg", url, StringComparison.Ordinal);
        Assert.Equal([Path.GetFileName(path)], this.StoredFiles());
    }

    /// <summary>
    /// Se a gravação das alterações falhar (por exemplo, conflito de concorrência), o ficheiro novo é apagado e a exceção
    /// segue para o tratador global, que a converte no ERR-41.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SaveFailureDeletesTheNewFileAndRethrows()
    {
        Job job = this.NewJob();
        this.store.SaveFailure = new ConcurrencyConflictException(new InvalidOperationException("Conflito."));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => this.service.AddPhotoAsync(this.recruiterId, job.Id, File("foto.jpg", Jpeg), TestContext.Current.CancellationToken));

        Assert.Empty(this.StoredFiles());
    }

    private static Company NewCompany(Guid recruiterId)
    {
        var company = new Company(
            recruiterId,
            new CompanyRegistration
            {
                CompanyName = "Lumen Software, Lda.",
                TaxId = "500100200",
                Industry = Industry.Technology,
                Address = "Rua de Exemplo, 1, 4700-000 Braga",
                LocationId = Guid.NewGuid(),
                ContactEmail = "geral@lumen-exemplo.pt",
                ContactPhone = "253000000",
                ResponsibleName = "Responsável Exemplo",
            },
            DateTimeOffset.UtcNow);
        company.Approve();
        return company;
    }

    private static Job NewJobFor(Guid companyId)
    {
        return new Job(
            companyId,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = Guid.NewGuid(),
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
    }

    private static byte[] Content(string kind)
    {
        return kind switch
        {
            "png" => Png,
            "jpeg" => Jpeg,
            "pdf" => Pdf,
            "gif" => "GIF89a"u8.ToArray(),
            "empty" => [],
            _ => "Isto é só texto, não uma imagem."u8.ToArray(),
        };
    }

    private static FormFile File(string fileName, byte[] content)
    {
        return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName);
    }

    private Job NewJob(JobStatus status = JobStatus.Unpublished, int photos = 0)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Job job = NewJobFor(this.company.Id);
        for (int index = 0; index < photos; index++)
        {
            job.AddPhoto($"job_photo/{index.ToString("x32", CultureInfo.InvariantCulture)}.jpg");
        }

        switch (status)
        {
            case JobStatus.Published:
                job.Publish(now);
                break;
            case JobStatus.Suspended:
                job.Publish(now);
                job.Suspend(now);
                break;
            case JobStatus.Closed:
                job.Close();
                break;
            default:
                break;
        }

        this.store.Jobs.Add(job);
        return job;
    }

    private List<string> StoredFiles()
    {
        string folder = Path.Combine(this.root, "job_photo");
        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToList()
            : [];
    }

    private sealed class Store : IJobRepository, ICompanyRepository, IUnitOfWork
    {
        public List<Job> Jobs { get; } = [];

        public List<Company> Companies { get; } = [];

        public Exception? SaveFailure { get; set; }

        public int Saves { get; private set; }

        public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Jobs.Find(job => job.Id == id && job.DeletedAt is null));
        }

        public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Companies.Find(item => item.RecruiterId == recruiterId));
        }

        Task<Company?> ICompanyRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken)
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

        public void DiscardChanges()
        {
            throw new NotSupportedException();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<JobWithCompanyStatus?> GetWithCompanyStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Job job, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Job>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> HasInterestsAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<EligibleJob>> ListEligibleForCandidateAsync(
            Candidate candidate,
            DateTimeOffset now,
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
