// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de <c>GET /api/files/{kind}/{fileName}</c> (documentação da API v02, secção 6.9.1; RNF007, RF061), sobre
/// SQLite in-memory e com o armazenamento numa pasta temporária.
/// </summary>
public sealed class FilesEndpointsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesEndpointsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public FilesEndpointsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// O logótipo, a fotografia da Empresa e a fotografia da vaga são obtidos por qualquer conta, com o tipo de conteúdo real.
    /// </summary>
    /// <param name="route">Tipo na rota.</param>
    /// <param name="folder">Pasta de armazenamento.</param>
    /// <param name="extension">Extensão do ficheiro.</param>
    /// <param name="contentType">Tipo de conteúdo esperado.</param>
    /// <param name="account">Tipo de conta que pede.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("COMPANY_LOGO", "company_logo", ".png", "image/png", UserType.Candidate)]
    [InlineData("COMPANY_LOGO", "company_logo", ".png", "image/png", UserType.Recruiter)]
    [InlineData("COMPANY_LOGO", "company_logo", ".png", "image/png", UserType.Admin)]
    [InlineData("COMPANY_PHOTO", "company_photo", ".jpg", "image/jpeg", UserType.Candidate)]
    [InlineData("COMPANY_PHOTO", "company_photo", ".jpg", "image/jpeg", UserType.Recruiter)]
    [InlineData("COMPANY_PHOTO", "company_photo", ".jpg", "image/jpeg", UserType.Admin)]
    [InlineData("JOB_PHOTO", "job_photo", ".png", "image/png", UserType.Candidate)]
    [InlineData("JOB_PHOTO", "job_photo", ".png", "image/png", UserType.Recruiter)]
    [InlineData("JOB_PHOTO", "job_photo", ".png", "image/png", UserType.Admin)]
    public async Task PublicKindsAreGotByAnyAccount(string route, string folder, string extension, string contentType, UserType account)
    {
        byte[] content = extension == ".png" ? CandidateProfileTestData.Png : CandidateProfileTestData.Jpeg;
        string name = await CandidateProfileTestData.StoreFileAsync(this.factory, folder, extension, content);
        string token = await this.TokenAsync(account);

        using HttpResponseMessage response = await this.GetAsync($"{route}/{name}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(content, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A fotografia de perfil é obtida pelo próprio Candidato.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ProfilePhotoIsGotByTheOwner()
    {
        (_, string token, string url) = await this.SeedCandidateWithPhotoAsync();

        using HttpResponseMessage response = await this.GetUrlAsync(url, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// A fotografia de perfil é obtida pelo Recrutador cuja vaga tem o interesse em espera ou o match.
    /// </summary>
    /// <param name="status">Estado da resposta do Candidato à vaga.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(MatchStatus.Waiting)]
    [InlineData(MatchStatus.Matched)]
    public async Task ProfilePhotoIsGotByTheRecruiterWithAnInterest(MatchStatus status)
    {
        (Candidate candidate, _, string url) = await this.SeedCandidateWithPhotoAsync();
        string recruiter = await CandidateProfileTestData.SeedInterestAsync(this.factory, candidate.Id, status);

        using HttpResponseMessage response = await this.GetUrlAsync(url, recruiter);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A fotografia de perfil é recusada (404) a outro Candidato, ao Recrutador sem interesse, ao Recrutador cujo único
    /// interesse é uma recusa do Candidato e ao Administrador.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ProfilePhotoIsRefusedToEveryoneElse()
    {
        (Candidate candidate, _, string url) = await this.SeedCandidateWithPhotoAsync();
        (_, _, string otherCandidate) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        string withoutInterest = await this.TokenAsync(UserType.Recruiter);
        string declined = await CandidateProfileTestData.SeedInterestAsync(this.factory, candidate.Id, MatchStatus.DeclinedByCandidate);
        string admin = await this.TokenAsync(UserType.Admin);

        foreach (string token in new[] { otherCandidate, withoutInterest, declined, admin })
        {
            using HttpResponseMessage response = await this.GetUrlAsync(url, token);

            await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
        }
    }

    /// <summary>
    /// O curriculum vitae nunca é obtido por esta operação, nem pelo próprio Candidato, e as respostas de tudo o que é
    /// recusado são iguais: tipo inválido, nome inexistente, nome de outro tipo e percursos de diretório.
    /// </summary>
    /// <param name="relativeUrl">Parte do URL depois de <c>/api/files/</c>.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("CURRICULUM_VITAE/{cv}")]
    [InlineData("curriculum_vitae/{cv}")]
    [InlineData("NAO_EXISTE/{logo}")]
    [InlineData("company_logo/{logo}")]
    [InlineData("COMPANY_LOGO/00000000000000000000000000000000.png")]
    [InlineData("COMPANY_PHOTO/{logo}")]
    [InlineData("COMPANY_LOGO/..%2f{logo}")]
    [InlineData("COMPANY_LOGO/..%5c{logo}")]
    [InlineData("COMPANY_LOGO/%2e%2e%2fcompany_logo%2f{logo}")]
    [InlineData("COMPANY_LOGO/C:%5cWindows%5cwin.ini")]
    [InlineData("COMPANY_LOGO/%2fetc%2fpasswd")]
    public async Task EverythingRefusedGivesTheSameErr20(string relativeUrl)
    {
        string logo = await CandidateProfileTestData.StoreFileAsync(this.factory, "company_logo", ".png", CandidateProfileTestData.Png);
        string cv = await CandidateProfileTestData.StoreFileAsync(this.factory, "curriculum_vitae", ".pdf", CandidateProfileTestData.Pdf);
        string token = await this.TokenAsync(UserType.Candidate);

        using HttpResponseMessage response = await this.GetAsync(
            relativeUrl.Replace("{logo}", logo, StringComparison.Ordinal).Replace("{cv}", cv, StringComparison.Ordinal),
            token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
    }

    /// <summary>
    /// Um ficheiro na pasta de outro nível não é alcançado com <c>..</c> a partir da pasta do tipo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DirectoryTraversalDoesNotReachFilesOutsideTheTypeFolder()
    {
        string secret = Guid.NewGuid().ToString("N") + ".png";
        await File.WriteAllBytesAsync(Path.Combine(this.factory.FileStorageRoot, secret), CandidateProfileTestData.Png, TestContext.Current.CancellationToken);
        string token = await this.TokenAsync(UserType.Candidate);

        using HttpResponseMessage response = await this.GetAsync($"COMPANY_LOGO/..%2f{secret}", token);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
    }

    /// <summary>
    /// Sem credencial a resposta é o ERR-01.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MissingTokenReturnsErr01()
    {
        using HttpResponseMessage response = await this.GetAsync("COMPANY_LOGO/00000000000000000000000000000000.png", token: null);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            "ERR-01",
            "A sessão expirou ou não é válida. Inicie sessão novamente.");
    }

    private async Task<string> TokenAsync(UserType userType)
    {
        if (userType == UserType.Candidate)
        {
            (_, _, string candidateToken) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
            return candidateToken;
        }

        (_, string token) = await CandidateProfileTestData.SeedAccountAsync(this.factory, userType);
        return token;
    }

    private async Task<(Candidate Candidate, string Token, string Url)> SeedCandidateWithPhotoAsync()
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        using HttpResponseMessage upload = await CandidateProfileTestData.SendAsync(
            this.factory,
            HttpMethod.Put,
            "/api/candidate/profile/photo",
            token,
            CandidateProfileTestData.Multipart("foto.png", CandidateProfileTestData.Png));
        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
        return (candidate, token, Assert.Single(upload.Headers.GetValues("Location")));
    }

    private Task<HttpResponseMessage> GetAsync(string relativeUrl, string? token)
    {
        return this.GetUrlAsync("/api/files/" + relativeUrl, token);
    }

    private Task<HttpResponseMessage> GetUrlAsync(string url, string? token)
    {
        return CandidateProfileTestData.SendAsync(this.factory, HttpMethod.Get, url, token);
    }
}
