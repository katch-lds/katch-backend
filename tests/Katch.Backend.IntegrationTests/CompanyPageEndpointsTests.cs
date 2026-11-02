// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Text;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes dos dados de registo e da página de apresentação da Empresa (documentação da API v02, secções 6.4.4 a 6.4.9 e
/// 6.3.3; RF017, RF046 a RF049, RF112; P01, P08, P10, P13), pela API e sobre a base de dados em memória.
/// </summary>
public sealed class CompanyPageEndpointsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string CompanyRoute = "/api/recruiter/company";
    private const string PageRoute = "/api/recruiter/company/page";
    private const string LogoRoute = "/api/recruiter/company/logo";
    private const string PhotosRoute = "/api/recruiter/company/photos";
    private const string CandidateRoute = "/api/candidate/jobs/companies/";
    private const string ValidationMessage = "Os dados enviados não cumprem a validação automática.";
    private const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";

    // Operações que exigem a Empresa aprovada (política ApprovedCompany): método e rota.
    private static readonly (string Method, string Route)[] ApprovedCompanyOperations =
    [
        ("PUT", CompanyRoute),
        ("GET", PageRoute),
        ("PUT", PageRoute),
        ("PUT", LogoRoute),
        ("POST", PhotosRoute),
    ];

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyPageEndpointsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public CompanyPageEndpointsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Gets the states of a company that is not approved, with the label of the ERR-07 message.
    /// </summary>
    public static TheoryData<CompanyStatus, string> NotApprovedStates => new()
    {
        { CompanyStatus.Pending, "pendente" },
        { CompanyStatus.Rejected, "recusada" },
        { CompanyStatus.Suspended, "suspensa" },
    };

    /// <summary>
    /// Os dados de registo são lidos em qualquer estado da Empresa, com a localidade e a data de submissão (6.4.8).
    /// </summary>
    /// <param name="status">Estado da Empresa.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending)]
    [InlineData(CompanyStatus.Approved)]
    [InlineData(CompanyStatus.Rejected)]
    [InlineData(CompanyStatus.Suspended)]
    public async Task RegistrationIsReadInAnyState(CompanyStatus status)
    {
        (Company company, Location location, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, status);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, CompanyRoute, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(company.Id, root.GetProperty("companyId").GetGuid());
        Assert.Equal("Lumen Software, Lda.", root.GetProperty("companyName").GetString());
        Assert.Equal(company.TaxId, root.GetProperty("taxId").GetString());
        Assert.Equal("TECHNOLOGY", root.GetProperty("industry").GetString());
        Assert.Equal(location.Id, root.GetProperty("locationId").GetGuid());
        Assert.Equal($"{location.Name} (Braga)", root.GetProperty("locationName").GetString());
        Assert.Equal("geral@lumen-exemplo.pt", root.GetProperty("contactEmail").GetString());
        Assert.Equal("253000111", root.GetProperty("contactPhone").GetString());
        Assert.Equal("Rui Matos", root.GetProperty("responsibleName").GetString());
        Assert.EndsWith("Z", root.GetProperty("submittedAt").GetString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Sem Empresa registada, a consulta dos dados de registo responde 404 ERR-20.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RegistrationWithoutACompanyReturnsErr20()
    {
        (_, string token) = await CandidateProfileTestData.SeedAccountAsync(this.factory, UserType.Recruiter);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, CompanyRoute, token);

        await CompanyRegistrationTestData.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
    }

    /// <summary>
    /// Com a Empresa aprovada, a alteração do contacto telefónico é gravada, a Empresa mantém-se aprovada e fica
    /// registado <c>COMPANY_UPDATED</c> com o Recrutador como autor (RF049, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateOfTheApprovedCompanyReturns200AndLogsTheChange()
    {
        (Company company, Location location, AppUser recruiter, string token) =
            await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            CompanyRoute,
            token,
            Json(Registration(company.TaxId, location.Id, contactPhone: "253000222")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(company.Id, root.GetProperty("companyId").GetGuid());
        Assert.Equal("APPROVED", root.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("rejectionReason").ValueKind);
        Company saved = await this.ReadCompanyAsync(company.Id);
        Assert.Equal("253000222", saved.ContactPhone);
        Assert.Equal("Rua Nova de Santa Cruz, 15", saved.Address);
        Assert.Equal(CompanyStatus.Approved, saved.Status);
        Assert.Equal(
            [(recruiter.Id, OperationType.CompanyUpdated)],
            await CompanyRegistrationTestData.ReadOperationsAsync(this.factory, company.Id));
    }

    /// <summary>
    /// A alteração do NIF da Empresa aprovada responde 400 ERR-10 com o motivo do catálogo, o valor anterior mantém-se
    /// e nada fica registado (RF049).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ChangeOfTheTaxIdReturnsErr10AndKeepsTheData()
    {
        (Company company, Location location, _, string token) =
            await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            CompanyRoute,
            token,
            Json(Registration("509999990", location.Id, contactPhone: "253000222")));

        JsonElement error = await CompanyRegistrationTestData.AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "ERR-10",
            ValidationMessage);
        Assert.Equal(
            [("taxId", "O número de identificação fiscal não pode ser alterado depois da aprovação.")],
            AuthTestAccounts.Fields(error));
        Company saved = await this.ReadCompanyAsync(company.Id);
        Assert.Equal(company.TaxId, saved.TaxId);
        Assert.Equal("253000111", saved.ContactPhone);
        Assert.Empty(await CompanyRegistrationTestData.ReadOperationsAsync(this.factory, company.Id));
    }

    /// <summary>
    /// A validação automática da alteração indica todos os campos em erro, incluindo a localidade fora da lista (RF042).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InvalidUpdateReportsEveryField()
    {
        (Company company, _, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            CompanyRoute,
            token,
            Json(Registration(company.TaxId, Guid.NewGuid(), contactPhone: "2530", contactEmail: "geral.lumen")));

        JsonElement error = await CompanyRegistrationTestData.AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10", ValidationMessage);
        Assert.Equal(
            [
                ("locationId", "O valor não pertence à lista pré-definida."),
                ("contactEmail", "O endereço de correio eletrónico tem de ter o formato local@domínio."),
                ("contactPhone", "O contacto telefónico tem de ter 9 algarismos."),
            ],
            AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// A página gravada é devolvida e lida depois, com os campos sem os espaços nas extremidades; <c>null</c> limpa o
    /// campo (RF046).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PageIsSavedReadAndCleared()
    {
        (Company company, _, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);

        using HttpResponseMessage saved = await this.SendAsync(
            HttpMethod.Put,
            PageRoute,
            token,
            Json(new { description = "  Software de gestão de frotas e armazéns.  ", website = "https://www.lumen-exemplo.pt" }));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        JsonElement page = await AuthTestAccounts.ReadJsonAsync(saved);
        Assert.Equal(company.Id, page.GetProperty("companyId").GetGuid());
        Assert.Equal("Lumen Software, Lda.", page.GetProperty("companyName").GetString());
        Assert.Equal("Software de gestão de frotas e armazéns.", page.GetProperty("description").GetString());
        Assert.Equal("https://www.lumen-exemplo.pt", page.GetProperty("website").GetString());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("logoUrl").ValueKind);
        Assert.Empty(page.GetProperty("photoUrls").EnumerateArray());

        using HttpResponseMessage read = await this.SendAsync(HttpMethod.Get, PageRoute, token);
        JsonElement readPage = await AuthTestAccounts.ReadJsonAsync(read);
        Assert.Equal("Software de gestão de frotas e armazéns.", readPage.GetProperty("description").GetString());

        using HttpResponseMessage cleared = await this.SendAsync(
            HttpMethod.Put,
            PageRoute,
            token,
            Json(new { description = (string?)null, website = (string?)null }));
        JsonElement clearedPage = await AuthTestAccounts.ReadJsonAsync(cleared);
        Assert.Equal(JsonValueKind.Null, clearedPage.GetProperty("description").ValueKind);
        Assert.Equal(JsonValueKind.Null, clearedPage.GetProperty("website").ValueKind);
        Assert.Null((await this.ReadCompanyAsync(company.Id)).Website);
    }

    /// <summary>
    /// A descrição acima de 1000 caracteres e o endereço sem <c>http://</c> nem <c>https://</c> respondem 400 ERR-10 com
    /// os dois campos e nada é gravado (P08, P10).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InvalidPageReturnsErr10WithBothFields()
    {
        (Company company, _, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            PageRoute,
            token,
            Json(new { description = new string('a', 1001), website = "www.lumen-exemplo.pt" }));

        JsonElement error = await CompanyRegistrationTestData.AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10", ValidationMessage);
        Assert.Equal(
            [
                ("description", "O texto não pode exceder 1000 caracteres."),
                ("website", "O endereço tem de começar por http:// ou https://."),
            ],
            AuthTestAccounts.Fields(error));
        Assert.Null((await this.ReadCompanyAsync(company.Id)).Description);
    }

    /// <summary>
    /// Um PNG de 1 MB é aceite como logótipo: 204 com o URL no <c>Location</c>, nome gerado pelo servidor, ficheiro no
    /// armazenamento e servido por <c>/api/files</c> a qualquer conta; um segundo logótipo substitui e apaga o anterior
    /// (RF047).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LogoIsStoredServedAndReplaced()
    {
        (Company company, _, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);
        byte[] logo = CandidateProfileTestData.WithLength(CandidateProfileTestData.Png, CandidateProfileTestData.Megabyte);

        using HttpResponseMessage first = await this.SendAsync(HttpMethod.Put, LogoRoute, token, CandidateProfileTestData.Multipart("logotipo.png", logo));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        string url = first.Headers.Location!.OriginalString;
        Assert.Matches("^/api/files/COMPANY_LOGO/[0-9a-f]{32}\\.png$", url);
        string firstPath = (await this.ReadCompanyAsync(company.Id)).LogoPath!;
        Assert.Equal("company_logo/" + url["/api/files/COMPANY_LOGO/".Length..], firstPath);
        Assert.True(File.Exists(Path.Combine(this.factory.FileStorageRoot, firstPath)));

        string candidateToken = await CompanyRegistrationTestData.SeedCandidateTokenAsync(this.factory);
        using HttpResponseMessage served = await this.SendAsync(HttpMethod.Get, url, candidateToken);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal(logo.Length, (await served.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length);

        using HttpResponseMessage second = await this.SendAsync(HttpMethod.Put, LogoRoute, token, CandidateProfileTestData.Multipart("novo.jpg", CandidateProfileTestData.Jpeg));
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        string secondPath = (await this.ReadCompanyAsync(company.Id)).LogoPath!;
        Assert.NotEqual(firstPath, secondPath);
        Assert.False(File.Exists(Path.Combine(this.factory.FileStorageRoot, firstPath)));
        Assert.True(File.Exists(Path.Combine(this.factory.FileStorageRoot, secondPath)));
    }

    /// <summary>
    /// Um logótipo acima de 2 MB (o PNG de 3 MB do critério do RF047), noutro formato ou com conteúdo que não
    /// corresponde à extensão responde 400 ERR-11 com o motivo, e nada é guardado (P01, RNF016).
    /// </summary>
    /// <param name="fileName">Nome enviado.</param>
    /// <param name="kind">Conteúdo: png, png2mb, png3mb ou gif.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("logo.png", "png3mb", "O ficheiro excede 2 MB.")]
    [InlineData("logo.png", "png2mb+1", "O ficheiro excede 2 MB.")]
    [InlineData("logo.gif", "gif", "O ficheiro tem de estar no formato JPEG ou PNG.")]
    [InlineData("logo.png", "gif", "O ficheiro tem de estar no formato JPEG ou PNG.")]
    [InlineData("logo.jpg", "png", "O ficheiro tem de estar no formato JPEG ou PNG.")]
    public async Task InvalidLogoReturnsErr11(string fileName, string kind, string reason)
    {
        (Company company, _, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, LogoRoute, token, CandidateProfileTestData.Multipart(fileName, Content(kind)));

        await AssertFileRejectedAsync(response, reason);
        Assert.Null((await this.ReadCompanyAsync(company.Id)).LogoPath);
    }

    /// <summary>
    /// Um logótipo de 2 MB exatos é aceite (o limite é inclusivo).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LogoOfExactlyTwoMegabytesIsAccepted()
    {
        (_, _, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, LogoRoute, token, CandidateProfileTestData.Multipart("logo.png", Content("png2mb")));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Os carregamentos acima de 6 MB respondem 413 ERR-12 e os que não são <c>multipart/form-data</c> 415 ERR-13.
    /// </summary>
    /// <param name="method">Método do pedido.</param>
    /// <param name="route">Rota do carregamento.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("PUT", LogoRoute)]
    [InlineData("POST", PhotosRoute)]
    public async Task UploadsAbove6MbReturnErr12AndNonMultipartReturnErr13(string method, string route)
    {
        (_, _, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);
        byte[] content = CandidateProfileTestData.WithLength(CandidateProfileTestData.Png, (6 * CandidateProfileTestData.Megabyte) + 8192);

        using HttpResponseMessage tooLarge = await this.SendAsync(new HttpMethod(method), route, token, CandidateProfileTestData.Multipart("grande.png", content));
        using HttpResponseMessage notMultipart = await this.SendAsync(new HttpMethod(method), route, token, Json(new { }));

        await CompanyRegistrationTestData.AssertErrorAsync(
            tooLarge,
            HttpStatusCode.RequestEntityTooLarge,
            "ERR-12",
            "O pedido excede a dimensão máxima permitida.");
        await CompanyRegistrationTestData.AssertErrorAsync(
            notMultipart,
            HttpStatusCode.UnsupportedMediaType,
            "ERR-13",
            "O tipo de conteúdo do pedido não é suportado.");
    }

    /// <summary>
    /// As fotografias são acrescentadas no fim da galeria (201 com o URL no <c>Location</c>) e aparecem na página pela
    /// ordem; com 6 fotografias, a sétima responde 409 ERR-39 e não é guardada (RF048, P13).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GalleryAcceptsSixPhotosAndRejectsTheSeventhWithErr39()
    {
        (Company company, _, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);
        var urls = new List<string>();
        for (int index = 0; index < Company.MaxGalleryPhotos; index++)
        {
            byte[] content = index % 2 == 0 ? CandidateProfileTestData.Jpeg : CandidateProfileTestData.Png;
            using HttpResponseMessage added = await this.SendAsync(HttpMethod.Post, PhotosRoute, token, CandidateProfileTestData.Multipart(index % 2 == 0 ? "foto.jpg" : "foto.png", content));
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
            urls.Add(added.Headers.Location!.OriginalString);
        }

        using HttpResponseMessage seventh = await this.SendAsync(HttpMethod.Post, PhotosRoute, token, CandidateProfileTestData.Multipart("foto.png", CandidateProfileTestData.Png));

        await CompanyRegistrationTestData.AssertErrorAsync(
            seventh,
            HttpStatusCode.Conflict,
            "ERR-39",
            "Foi atingido o número máximo de fotografias (6).");
        Assert.All(urls, url => Assert.Matches("^/api/files/COMPANY_PHOTO/[0-9a-f]{32}\\.(jpg|png)$", url));
        Company saved = await this.ReadCompanyAsync(company.Id);
        Assert.Equal(Company.MaxGalleryPhotos, saved.PhotoPaths.Count);
        Assert.All(saved.PhotoPaths, path => Assert.True(File.Exists(Path.Combine(this.factory.FileStorageRoot, path))));

        using HttpResponseMessage page = await this.SendAsync(HttpMethod.Get, PageRoute, token);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(page);
        Assert.Equal(urls, root.GetProperty("photoUrls").EnumerateArray().Select(url => url.GetString()!));
    }

    /// <summary>
    /// Uma fotografia da galeria acima de 5 MB ou num formato que não é JPEG nem PNG responde 400 ERR-11; uma de 5 MB
    /// exatos é aceite (RF048, P01).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GalleryPhotoAboveFiveMegabytesOrGifReturnsErr11()
    {
        (Company company, _, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);
        byte[] limit = CandidateProfileTestData.WithLength(CandidateProfileTestData.Jpeg, 5 * CandidateProfileTestData.Megabyte);

        using HttpResponseMessage tooLarge = await this.SendAsync(HttpMethod.Post, PhotosRoute, token, CandidateProfileTestData.Multipart("foto.jpg", [.. limit, 0]));
        using HttpResponseMessage gif = await this.SendAsync(HttpMethod.Post, PhotosRoute, token, CandidateProfileTestData.Multipart("foto.gif", Content("gif")));
        using HttpResponseMessage accepted = await this.SendAsync(HttpMethod.Post, PhotosRoute, token, CandidateProfileTestData.Multipart("foto.jpg", limit));

        await AssertFileRejectedAsync(tooLarge, "O ficheiro excede 5 MB.");
        await AssertFileRejectedAsync(gif, "O ficheiro tem de estar no formato JPEG ou PNG.");
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Single((await this.ReadCompanyAsync(company.Id)).PhotoPaths);
    }

    /// <summary>
    /// Com a Empresa pendente, recusada ou suspensa, a alteração dos dados e as operações da página, do logótipo e da
    /// galeria respondem 403 ERR-07 com o estado na mensagem (RF039).
    /// </summary>
    /// <param name="status">Estado da Empresa.</param>
    /// <param name="label">Estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(NotApprovedStates))]
    public async Task OperationsOnACompanyThatIsNotApprovedReturnErr07(CompanyStatus status, string label)
    {
        (Company company, Location location, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, status);

        foreach ((string method, string route) in ApprovedCompanyOperations)
        {
            using HttpResponseMessage response = await this.SendAsync(new HttpMethod(method), route, token, Body(method, route, company.TaxId, location.Id));

            await CompanyRegistrationTestData.AssertErrorAsync(
                response,
                HttpStatusCode.Forbidden,
                "ERR-07",
                $"A operação exige a Empresa aprovada. Estado atual da Empresa: {label}.");
        }

        Company saved = await this.ReadCompanyAsync(company.Id);
        Assert.Null(saved.LogoPath);
        Assert.Empty(saved.PhotoPaths);
        Assert.Equal("253000111", saved.ContactPhone);
    }

    /// <summary>
    /// Um Recrutador sem Empresa registada recebe o ERR-07 com o estado «não registada».
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OperationsWithoutACompanyReturnErr07()
    {
        (_, string token) = await CandidateProfileTestData.SeedAccountAsync(this.factory, UserType.Recruiter);

        foreach ((string method, string route) in ApprovedCompanyOperations)
        {
            using HttpResponseMessage response = await this.SendAsync(new HttpMethod(method), route, token, Body(method, route, "509123456", Guid.NewGuid()));

            await CompanyRegistrationTestData.AssertErrorAsync(
                response,
                HttpStatusCode.Forbidden,
                "ERR-07",
                "A operação exige a Empresa aprovada. Estado atual da Empresa: não registada.");
        }
    }

    /// <summary>
    /// O Candidato e o Administrador não acedem às operações do Recrutador sobre a Empresa (ERR-06), e o Recrutador e o
    /// Administrador não acedem à consulta do Candidato (RNF006).
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Admin)]
    [InlineData(UserType.Recruiter)]
    public async Task OtherAccountTypesReturnErr06(UserType userType)
    {
        (Company company, Location location, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);
        (_, string token) = await CandidateProfileTestData.SeedAccountAsync(this.factory, userType);

        var routes = new List<(string Method, string Route)>();
        if (userType == UserType.Recruiter)
        {
            routes.Add(("GET", CandidateRoute + company.Id));
        }
        else
        {
            routes.Add(("GET", CompanyRoute));
            routes.AddRange(ApprovedCompanyOperations);
        }

        foreach ((string method, string route) in routes)
        {
            using HttpResponseMessage response = await this.SendAsync(new HttpMethod(method), route, token, Body(method, route, company.TaxId, location.Id));

            await CompanyRegistrationTestData.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-06", Err06Message);
        }
    }

    /// <summary>
    /// Sem credencial, as operações respondem 401 ERR-01.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RequestsWithoutCredentialReturnErr01()
    {
        foreach (string route in new[] { CompanyRoute, PageRoute, CandidateRoute + Guid.NewGuid() })
        {
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, route, token: null);

            await CompanyRegistrationTestData.AssertErrorAsync(
                response,
                HttpStatusCode.Unauthorized,
                "ERR-01",
                "A sessão expirou ou não é válida. Inicie sessão novamente.");
        }
    }

    /// <summary>
    /// A alteração de um Recrutador só toca na própria Empresa: a de outro Recrutador fica igual.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RecruiterOnlyChangesTheOwnCompany()
    {
        (_, _, _, string token) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);
        (Company other, _, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            PageRoute,
            token,
            Json(new { description = "Página do primeiro Recrutador.", website = (string?)null }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(other.Id, (await AuthTestAccounts.ReadJsonAsync(response)).GetProperty("companyId").GetGuid());
        Assert.Null((await this.ReadCompanyAsync(other.Id)).Description);
    }

    /// <summary>
    /// O Candidato consulta a página de uma Empresa aprovada, com o logótipo e a galeria, sem os contactos, que só vê
    /// depois do match (6.3.3; RF017, RF025).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CandidateReadsThePageOfAnApprovedCompanyWithoutContacts()
    {
        (Company company, _, _, string recruiterToken) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);
        using HttpResponseMessage page = await this.SendAsync(
            HttpMethod.Put,
            PageRoute,
            recruiterToken,
            Json(new { description = "Software de gestão de frotas.", website = "https://www.lumen-exemplo.pt" }));
        using HttpResponseMessage logo = await this.SendAsync(HttpMethod.Put, LogoRoute, recruiterToken, CandidateProfileTestData.Multipart("logo.png", CandidateProfileTestData.Png));
        using HttpResponseMessage photo = await this.SendAsync(HttpMethod.Post, PhotosRoute, recruiterToken, CandidateProfileTestData.Multipart("foto.jpg", CandidateProfileTestData.Jpeg));
        string candidateToken = await CompanyRegistrationTestData.SeedCandidateTokenAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, CandidateRoute + company.Id, candidateToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(
            ["companyId", "companyName", "description", "website", "logoUrl", "photoUrls"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(company.Id, root.GetProperty("companyId").GetGuid());
        Assert.Equal("Software de gestão de frotas.", root.GetProperty("description").GetString());
        Assert.Equal("https://www.lumen-exemplo.pt", root.GetProperty("website").GetString());
        Assert.Equal(logo.Headers.Location!.OriginalString, root.GetProperty("logoUrl").GetString());
        Assert.Equal([photo.Headers.Location!.OriginalString], root.GetProperty("photoUrls").EnumerateArray().Select(url => url.GetString()!));
        Assert.DoesNotContain("253000111", root.GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A página de uma Empresa pendente, recusada ou suspensa, inexistente ou com identificador mal formado responde ao
    /// Candidato 404 ERR-20 (6.3.3).
    /// </summary>
    /// <param name="status">Estado da Empresa; <see langword="null"/> para uma Empresa inexistente.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending)]
    [InlineData(CompanyStatus.Rejected)]
    [InlineData(CompanyStatus.Suspended)]
    [InlineData(null)]
    public async Task CandidateCannotReadThePageOfACompanyThatIsNotApproved(CompanyStatus? status)
    {
        Guid companyId = status is null
            ? Guid.NewGuid()
            : (await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, status.Value)).Company.Id;
        string candidateToken = await CompanyRegistrationTestData.SeedCandidateTokenAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, CandidateRoute + companyId, candidateToken);
        using HttpResponseMessage malformed = await this.SendAsync(HttpMethod.Get, CandidateRoute + "nao-e-um-guid", candidateToken);

        await CompanyRegistrationTestData.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    private static object Registration(
        string taxId,
        Guid locationId,
        string contactPhone = "253000111",
        string contactEmail = "recrutamento@lumen-exemplo.pt")
    {
        return new
        {
            companyName = "Lumen Software, Lda.",
            taxId,
            industry = "TECHNOLOGY",
            address = "  Rua Nova de Santa Cruz, 15  ",
            locationId,
            contactEmail,
            contactPhone,
            responsibleName = "Rui Matos",
        };
    }

    private static HttpContent? Body(string method, string route, string taxId, Guid locationId)
    {
        if (method == "GET")
        {
            return null;
        }

        return route switch
        {
            CompanyRoute => Json(Registration(taxId, locationId, contactPhone: "253000222")),
            PageRoute => Json(new { description = "Descrição.", website = (string?)null }),
            LogoRoute or PhotosRoute => CandidateProfileTestData.Multipart("foto.png", CandidateProfileTestData.Png),
            _ => Json(new { }),
        };
    }

    private static StringContent Json(object body)
    {
        return new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
    }

    private static byte[] Content(string kind)
    {
        return kind switch
        {
            "png" => CandidateProfileTestData.Png,
            "png2mb" => CandidateProfileTestData.WithLength(CandidateProfileTestData.Png, 2 * CandidateProfileTestData.Megabyte),
            "png2mb+1" => CandidateProfileTestData.WithLength(CandidateProfileTestData.Png, (2 * CandidateProfileTestData.Megabyte) + 1),
            "png3mb" => CandidateProfileTestData.WithLength(CandidateProfileTestData.Png, 3 * CandidateProfileTestData.Megabyte),
            "gif" => [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Conteúdo de teste desconhecido."),
        };
    }

    private static async Task AssertFileRejectedAsync(HttpResponseMessage response, string reason)
    {
        JsonElement error = await CompanyRegistrationTestData.AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "ERR-11",
            "O ficheiro não é aceite.");
        Assert.Equal([("file", reason)], AuthTestAccounts.Fields(error));
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, HttpContent? content = null)
    {
        return CandidateProfileTestData.SendAsync(this.factory, method, path, token, content);
    }

    private async Task<Company> ReadCompanyAsync(Guid companyId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Companies.AsNoTracking().SingleAsync(company => company.Id == companyId, TestContext.Current.CancellationToken);
    }
}
