// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Katch.Backend.IntegrationTests.CandidateProfileTestData;
using static Katch.Backend.IntegrationTests.JobPhotoTestData;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de <c>POST /api/recruiter/jobs/{jobId}/photos</c> (documentação da API v02, secção 6.5.9; RF052, RF059;
/// RNF015, RNF016; P01, P13, P19), sobre SQLite in-memory e um armazenamento temporário da fábrica.
/// </summary>
public sealed partial class JobPhotoEndpointsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string FileTypeReason = "O ficheiro tem de estar no formato JPEG ou PNG.";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobPhotoEndpointsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação de teste partilhada.</param>
    public JobPhotoEndpointsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Uma fotografia válida devolve <c>201</c> sem corpo, com o URL no <c>Location</c>, fica em <c>photo_paths</c> com
    /// um nome gerado pelo servidor e o ficheiro fica no armazenamento.
    /// </summary>
    /// <param name="fileName">Nome enviado.</param>
    /// <param name="kind">Conteúdo enviado: jpeg ou png.</param>
    /// <param name="extension">Extensão esperada do ficheiro guardado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("equipa.jpg", "jpeg", ".jpg")]
    [InlineData("equipa.png", "png", ".png")]
    public async Task ValidPhotoReturns201WithLocationAndIsStored(string fileName, string kind, string extension)
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed);

        using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, fileName, Content(kind));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        string location = Assert.Single(response.Headers.GetValues("Location"));
        string path = Assert.Single(await ReadPhotoPathsAsync(this.factory, jobId));
        Assert.Matches(StoredPathPattern(), path);
        Assert.EndsWith(extension, path, StringComparison.Ordinal);
        Assert.Equal($"/api/files/JOB_PHOTO/{path["job_photo/".Length..]}", location);
        Assert.True(File.Exists(Path.Combine(this.factory.FileStorageRoot, path)));
    }

    /// <summary>
    /// As fotografias ficam pela ordem de carregamento, nos <c>photo_paths</c> e nos <c>photoUrls</c> da listagem
    /// (6.5.1), em URL da API.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PhotosKeepTheUploadOrderAndAppearInTheJobList()
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed, JobStatus.Published);
        var locations = new List<string>();
        foreach ((string kind, string name) in new[] { ("png", "a.png"), ("jpeg", "b.jpg"), ("png", "c.png") })
        {
            using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, name, Content(kind));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            locations.Add(Assert.Single(response.Headers.GetValues("Location")));
        }

        List<string> paths = await ReadPhotoPathsAsync(this.factory, jobId);
        Assert.Equal(locations, paths.Select(path => $"/api/files/JOB_PHOTO/{path["job_photo/".Length..]}"));

        using HttpResponseMessage list = await CandidateProfileTestData.SendAsync(this.factory, HttpMethod.Get, "/api/recruiter/jobs", seed.Token);
        JsonElement jobs = await AuthTestAccounts.ReadJsonAsync(list);
        JsonElement job = jobs.EnumerateArray().Single(item => item.GetProperty("jobId").GetGuid() == jobId);
        Assert.Equal(locations, job.GetProperty("photoUrls").EnumerateArray().Select(item => item.GetString()));
    }

    /// <summary>
    /// Cinco fotografias são aceites; a sexta recebe <c>409</c> ERR-39 com «(5)» e o ficheiro não fica no
    /// armazenamento (P13).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SixthPhotoReturnsErr39AndLeavesNoFile()
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed);
        for (int count = 1; count <= 5; count++)
        {
            using HttpResponseMessage accepted = await this.UploadAsync(jobId, seed.Token, "foto.jpg", Jpeg);
            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        }

        List<string> before = StoredPhotos(this.factory);
        using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, "sexta.jpg", Jpeg);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "ERR-39",
            "Foi atingido o número máximo de fotografias (5).");
        Assert.Equal(5, (await ReadPhotoPathsAsync(this.factory, jobId)).Count);
        Assert.Equal(before, StoredPhotos(this.factory));
    }

    /// <summary>
    /// Ficheiros que não são JPEG nem PNG verdadeiros recebem <c>400</c> ERR-11 e nada é gravado (RNF016).
    /// </summary>
    /// <param name="fileName">Nome enviado.</param>
    /// <param name="kind">Conteúdo enviado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("foto.jpg", "text")]
    [InlineData("foto.png", "pdf")]
    [InlineData("foto.gif", "gif")]
    [InlineData("foto.jpg", "gif")]
    [InlineData("foto.pdf", "png")]
    [InlineData("foto.png", "jpeg")]
    [InlineData("foto.jpg", "png")]
    [InlineData("foto", "png")]
    public async Task FilesWithTheWrongRealFormatReturnErr11(string fileName, string kind)
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed);
        List<string> before = StoredPhotos(this.factory);

        using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, fileName, Content(kind));

        await AssertFileRejectedAsync(response, FileTypeReason);
        Assert.Empty(await ReadPhotoPathsAsync(this.factory, jobId));
        Assert.Equal(before, StoredPhotos(this.factory));
    }

    /// <summary>
    /// 5 MB exatos são aceites; mais de 5 MB e até 6 MB dão ERR-11 com o motivo da dimensão (P01).
    /// </summary>
    /// <param name="length">Dimensão do ficheiro.</param>
    /// <param name="accepted">Se o ficheiro é aceite.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(5 * Megabyte, true)]
    [InlineData((5 * Megabyte) + 1, false)]
    [InlineData(6 * Megabyte, false)]
    public async Task FileSizeLimitIs5MbWithErr11AboveIt(int length, bool accepted)
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed);

        using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, "foto.jpg", WithLength(Jpeg, length));

        if (accepted)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        else
        {
            await AssertFileRejectedAsync(response, "O ficheiro excede 5 MB.");
            Assert.Empty(await ReadPhotoPathsAsync(this.factory, jobId));
        }
    }

    /// <summary>
    /// Um pedido acima de 6 MB responde <c>413</c> ERR-12. O limite é verificado pelo filtro de upload (Content-Length),
    /// porque o servidor de teste não aplica o limite do Kestrel.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RequestAbove6MbReturnsErr12()
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed);

        using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, "grande.jpg", WithLength(Jpeg, (6 * Megabyte) + 8192));

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.RequestEntityTooLarge,
            "ERR-12",
            "O pedido excede a dimensão máxima permitida.");
        Assert.Empty(await ReadPhotoPathsAsync(this.factory, jobId));
    }

    /// <summary>
    /// Um tipo de conteúdo que não é <c>multipart/form-data</c> responde <c>415</c> ERR-13.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NonMultipartRequestReturnsErr13()
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed);

        using var body = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await CandidateProfileTestData.SendAsync(this.factory, HttpMethod.Post, Route(jobId), seed.Token, body);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.UnsupportedMediaType,
            "ERR-13",
            "O tipo de conteúdo do pedido não é suportado.");
    }

    /// <summary>
    /// Sem o campo <c>file</c>, ou com um ficheiro vazio, a resposta é <c>400</c> ERR-11.
    /// </summary>
    /// <param name="fieldName">Nome do campo enviado.</param>
    /// <param name="length">Dimensão do ficheiro enviado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("outro", 10)]
    [InlineData("file", 0)]
    public async Task MissingOrEmptyFileReturnsErr11(string fieldName, int length)
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed);

        using MultipartFormDataContent form = Multipart("foto.jpg", new byte[length], fieldName);
        using HttpResponseMessage response = await CandidateProfileTestData.SendAsync(this.factory, HttpMethod.Post, Route(jobId), seed.Token, form);

        await AssertFileRejectedAsync(response, "O campo é obrigatório.");
    }

    /// <summary>
    /// O nome enviado pelo cliente nunca entra no caminho: o nome guardado é gerado pelo servidor.
    /// </summary>
    /// <param name="clientName">Nome enviado pelo cliente.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(@"..\..\x.jpg")]
    [InlineData("../../x.jpg")]
    [InlineData("job_photo/x.jpg")]
    [InlineData("x.jpg")]
    public async Task ClientFileNameNeverEntersThePath(string clientName)
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed);

        using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, clientName, Jpeg);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        string path = Assert.Single(await ReadPhotoPathsAsync(this.factory, jobId));
        Assert.Matches(StoredPathPattern(), path);
        Assert.DoesNotContain("x.jpg", path, StringComparison.Ordinal);
        Assert.Contains(path["job_photo/".Length..], StoredPhotos(this.factory));
        Assert.False(File.Exists(Path.Combine(this.factory.FileStorageRoot, "x.jpg")));
    }

    /// <summary>
    /// Uma vaga de outra Empresa recebe <c>403</c> ERR-08 e nada é gravado (RF059).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task JobOfAnotherCompanyReturnsErr08()
    {
        RecruiterSeed owner = await SeedRecruiterAsync(this.factory);
        RecruiterSeed other = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, owner);
        List<string> before = StoredPhotos(this.factory);

        using HttpResponseMessage response = await this.UploadAsync(jobId, other.Token, "foto.jpg", Jpeg);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Forbidden,
            "ERR-08",
            "Não tem autorização para aceder a este elemento.");
        Assert.Empty(await ReadPhotoPathsAsync(this.factory, jobId));
        Assert.Equal(before, StoredPhotos(this.factory));
    }

    /// <summary>
    /// Uma vaga inexistente ou eliminada recebe <c>404</c> ERR-20 e nenhum ficheiro fica gravado.
    /// </summary>
    /// <param name="deleted">Se a vaga existe mas está eliminada.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrDeletedJobReturnsErr20(bool deleted)
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = deleted ? await SeedJobAsync(this.factory, seed, deleted: true) : Guid.NewGuid();
        List<string> before = StoredPhotos(this.factory);

        using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, "foto.jpg", Jpeg);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.NotFound,
            "ERR-20",
            "O elemento pedido não existe ou não está disponível.");
        Assert.Equal(before, StoredPhotos(this.factory));
        if (deleted)
        {
            Assert.Empty(await ReadPhotoPathsAsync(this.factory, jobId));
        }
    }

    /// <summary>
    /// Uma vaga encerrada recebe <c>409</c> ERR-30 com o estado atual e nada é gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ClosedJobReturnsErr30()
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed, JobStatus.Closed);
        List<string> before = StoredPhotos(this.factory);

        using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, "foto.jpg", Jpeg);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "ERR-30",
            "A operação não é permitida no estado atual: encerrada.");
        Assert.Empty(await ReadPhotoPathsAsync(this.factory, jobId));
        Assert.Equal(before, StoredPhotos(this.factory));
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
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed, status);

        using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, "foto.jpg", Jpeg);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(await ReadPhotoPathsAsync(this.factory, jobId));
    }

    /// <summary>
    /// A vaga encerrada com o limite atingido recebe o ERR-30, e o limite atingido com um ficheiro inválido recebe o
    /// ERR-39: a ordem das verificações é titularidade, estado, limite e ficheiro.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ChecksRunInTheDocumentedOrder()
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid closedFull = await SeedJobAsync(this.factory, seed, JobStatus.Closed, photos: 5);
        Guid openFull = await SeedJobAsync(this.factory, seed, photos: 5);

        using HttpResponseMessage closed = await this.UploadAsync(closedFull, seed.Token, "foto.jpg", Content("text"));
        using HttpResponseMessage full = await this.UploadAsync(openFull, seed.Token, "foto.jpg", Content("text"));

        await AuthenticationTests.AssertErrorAsync(closed, HttpStatusCode.Conflict, "ERR-30", "A operação não é permitida no estado atual: encerrada.");
        await AuthenticationTests.AssertErrorAsync(full, HttpStatusCode.Conflict, "ERR-39", "Foi atingido o número máximo de fotografias (5).");
    }

    /// <summary>
    /// Só o Recrutador de uma Empresa aprovada tem acesso: ERR-07 com a Empresa pendente, recusada, suspensa ou
    /// inexistente; ERR-06 para o Candidato e o Administrador.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="companyStatus">Estado da Empresa, ou nulo sem Empresa.</param>
    /// <param name="code">Código esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Recruiter, CompanyStatus.Pending, "ERR-07")]
    [InlineData(UserType.Recruiter, CompanyStatus.Rejected, "ERR-07")]
    [InlineData(UserType.Recruiter, CompanyStatus.Suspended, "ERR-07")]
    [InlineData(UserType.Recruiter, null, "ERR-07")]
    [InlineData(UserType.Candidate, null, "ERR-06")]
    [InlineData(UserType.Admin, null, "ERR-06")]
    public async Task OnlyRecruitersOfApprovedCompaniesHaveAccess(UserType userType, CompanyStatus? companyStatus, string code)
    {
        RecruiterSeed owner = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, owner);
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, userType, companyStatus: companyStatus);
        string token = this.factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;

        using HttpResponseMessage response = await this.UploadAsync(jobId, token, "foto.jpg", Jpeg);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Empty(await ReadPhotoPathsAsync(this.factory, jobId));
    }

    /// <summary>
    /// Sem credencial, o pedido recebe <c>401</c> ERR-01.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AnonymousRequestReturnsErr01()
    {
        using HttpResponseMessage response = await this.UploadAsync(Guid.NewGuid(), token: null, "foto.jpg", Jpeg);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-01", error.GetProperty("code").GetString());
    }

    /// <summary>
    /// Com o <c>Location</c> devolvido, o Recrutador e um Candidato obtêm a fotografia (<c>200</c>, tipo correto).
    /// </summary>
    /// <param name="kind">Formato enviado: jpeg ou png.</param>
    /// <param name="contentType">Tipo de conteúdo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("png", "image/png")]
    public async Task UploadedPhotoCanBeFetchedByRecruiterAndCandidate(string kind, string contentType)
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed, JobStatus.Published);
        byte[] content = Content(kind);
        using HttpResponseMessage upload = await this.UploadAsync(jobId, seed.Token, kind == "png" ? "foto.png" : "foto.jpg", content);
        string location = Assert.Single(upload.Headers.GetValues("Location"));
        (_, _, string candidateToken) = await SeedCandidateAsync(this.factory);

        foreach (string token in new[] { seed.Token, candidateToken })
        {
            using HttpResponseMessage response = await CandidateProfileTestData.SendAsync(this.factory, HttpMethod.Get, location, token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(content, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }
    }

    /// <summary>
    /// O carregamento de um JPEG de 5 MB exatos termina em menos de 5 segundos (P19). O tempo é medido à volta do
    /// pedido, sem a preparação dos dados.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UploadOf5MbCompletesWithin5Seconds()
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed);
        byte[] content = WithLength(Jpeg, 5 * Megabyte);

        var stopwatch = Stopwatch.StartNew();
        using HttpResponseMessage response = await this.UploadAsync(jobId, seed.Token, "grande.jpg", content);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"O carregamento demorou {stopwatch.Elapsed.TotalSeconds:F2} s.");
    }

    /// <summary>
    /// Se o acrescento à vaga falhar com um conflito de concorrência, a resposta é <c>409</c> ERR-41 e o ficheiro novo é
    /// apagado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrencyConflictReturnsErr41AndDeletesTheNewFile()
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed);
        List<string> before = StoredPhotos(this.factory);

        using WebApplicationFactory<Program> failing = this.factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddScoped<IJobRepository>(provider => new FailingPhotoJobRepository(ActivatorUtilities.CreateInstance<JobRepository>(provider)))));
        using HttpClient client = failing.CreateClient();
        using HttpResponseMessage response = await SendWithAsync(client, HttpMethod.Post, Route(jobId), failing.Services.GetRequiredService<ITokenService>().CreateToken(seed.User).Token, Multipart("foto.jpg", Jpeg));

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "ERR-41",
            "O elemento foi alterado por outro pedido. Atualize e tente novamente.");
        Assert.Equal(before, StoredPhotos(this.factory));
        Assert.Empty(await ReadPhotoPathsAsync(this.factory, jobId));
    }

    /// <summary>
    /// A atualização atómica do repositório acrescenta os caminhos pela ordem, pára no limite de 5 sem os ultrapassar e
    /// recusa a vaga eliminada (SQL do SQLite dos testes; o do PostgreSQL é verificado contra a base real).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AtomicAddAppendsInOrderAndStopsAtTheLimit()
    {
        RecruiterSeed seed = await SeedRecruiterAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, seed, photos: 3);
        Guid deletedId = await SeedJobAsync(this.factory, seed, deleted: true);
        CancellationToken token = TestContext.Current.CancellationToken;

        using IServiceScope scope = this.factory.Services.CreateScope();
        IJobRepository jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        bool fourth = await jobs.TryAddPhotoAsync(jobId, "job_photo/quarta.jpg", Job.MaxPhotos, token);
        bool fifth = await jobs.TryAddPhotoAsync(jobId, "job_photo/quinta.jpg", Job.MaxPhotos, token);
        bool sixth = await jobs.TryAddPhotoAsync(jobId, "job_photo/sexta.jpg", Job.MaxPhotos, token);
        bool deleted = await jobs.TryAddPhotoAsync(deletedId, "job_photo/x.jpg", Job.MaxPhotos, token);

        Assert.Equal([true, true, false, false], [fourth, fifth, sixth, deleted]);
        List<string> paths = await ReadPhotoPathsAsync(this.factory, jobId);
        Assert.Equal(5, paths.Count);
        Assert.Equal(["job_photo/quarta.jpg", "job_photo/quinta.jpg"], paths.Skip(3));
        Assert.Empty(await ReadPhotoPathsAsync(this.factory, deletedId));
    }

    [GeneratedRegex(@"^job_photo/[0-9a-f]{32}\.(jpg|png)$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StoredPathPattern();

    private static string Route(Guid jobId)
    {
        return $"/api/recruiter/jobs/{jobId}/photos";
    }

    private static byte[] Content(string kind)
    {
        return kind switch
        {
            "png" => Png,
            "jpeg" => Jpeg,
            "pdf" => Pdf,
            "gif" => "GIF89a"u8.ToArray(),
            _ => "Isto é só texto, não uma imagem."u8.ToArray(),
        };
    }

    private static async Task AssertFileRejectedAsync(HttpResponseMessage response, string reason)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-11", error.GetProperty("code").GetString());
        Assert.Equal("O ficheiro não é aceite.", error.GetProperty("message").GetString());
        Assert.Equal([("file", reason)], AuthTestAccounts.Fields(error));
    }

    private static async Task<HttpResponseMessage> SendWithAsync(HttpClient client, HttpMethod method, string path, string token, HttpContent content)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> UploadAsync(Guid jobId, string? token, string fileName, byte[] content)
    {
        using MultipartFormDataContent form = Multipart(fileName, content);
        return await CandidateProfileTestData.SendAsync(this.factory, HttpMethod.Post, Route(jobId), token, form);
    }

    private sealed class FailingPhotoJobRepository(JobRepository inner) : IJobRepository
    {
        public Task<JobWithCompanyStatus?> GetWithCompanyStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return inner.GetWithCompanyStatusAsync(jobId, cancellationToken);
        }

        public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return inner.GetByIdAsync(id, cancellationToken);
        }

        public Task AddAsync(Job job, CancellationToken cancellationToken = default)
        {
            return inner.AddAsync(job, cancellationToken);
        }

        public Task<List<Job>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return inner.ListByCompanyAsync(companyId, cancellationToken);
        }

        public Task<bool> HasInterestsAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return inner.HasInterestsAsync(jobId, cancellationToken);
        }

        public Task<bool> TryAddPhotoAsync(Guid jobId, string path, int maxPhotos, CancellationToken cancellationToken = default)
        {
            throw new ConcurrencyConflictException(new InvalidOperationException("Conflito simulado."));
        }
    }
}
