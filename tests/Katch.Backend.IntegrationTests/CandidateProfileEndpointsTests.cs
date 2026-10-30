// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Text;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes dos endpoints do perfil profissional do Candidato (documentação da API v02, secções 6.2.1 a 6.2.3 e 6.2.6 a
/// 6.2.9), sobre SQLite in-memory e com o armazenamento numa pasta temporária.
/// </summary>
public sealed class CandidateProfileEndpointsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string ProfileRoute = "/api/candidate/profile";
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";
    private const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";
    private const string ValidationMessage = "Os dados enviados não cumprem a validação automática.";
    private const string FileRejectedMessage = "O ficheiro não é aceite.";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateProfileEndpointsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public CandidateProfileEndpointsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// A consulta de um perfil sem dados opcionais devolve os campos a <c>null</c>, as listas vazias e a localidade com o distrito.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetWithoutOptionalDataReturnsNullsAndEmptyLists()
    {
        (Candidate candidate, Location location, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, string.Empty, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(candidate.Id, root.GetProperty("candidateId").GetGuid());
        Assert.Equal("Ana Ferreira", root.GetProperty("fullName").GetString());
        Assert.Equal(candidate.User.Email, root.GetProperty("email").GetString());
        Assert.Equal($"{location.Name} (Braga)", root.GetProperty("locationName").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("desiredRole").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("availability").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("experienceSummary").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("photoUrl").ValueKind);
        Assert.False(root.GetProperty("hasCv").GetBoolean());
        Assert.Empty(root.GetProperty("links").EnumerateArray());
        Assert.Empty(root.GetProperty("skills").EnumerateArray());
        Assert.Empty(root.GetProperty("customSkills").EnumerateArray());
        JsonElement preferences = root.GetProperty("preferences");
        Assert.Equal(JsonValueKind.Null, preferences.GetProperty("maxDistanceKm").ValueKind);
        Assert.Empty(preferences.GetProperty("workModes").EnumerateArray());
    }

    /// <summary>
    /// A consulta de um perfil completo devolve os dados, as hiperligações pela ordem e as preferências.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetWithDataReturnsTheFullProfile()
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory, candidate =>
        {
            candidate.DesiredRole = "Programadora backend";
            candidate.Availability = Availability.OneMonth;
            candidate.ExperienceSummary = "Dois anos de APIs REST.";
            candidate.MaxDistanceKm = 50;
            candidate.MinSalaryExpectation = 1400m;
            candidate.PreferredWorkModes = [WorkMode.Hybrid, WorkMode.Remote];
            candidate.PreferredContractTypes = [ContractType.Permanent];
            candidate.SetLinks(["https://github.com/ana", "https://exemplo.pt/ana"]);
        });

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, string.Empty, token);

        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ONE_MONTH", root.GetProperty("availability").GetString());
        Assert.Equal("Programadora backend", root.GetProperty("desiredRole").GetString());
        Assert.Equal(
            ["https://github.com/ana", "https://exemplo.pt/ana"],
            root.GetProperty("links").EnumerateArray().Select(link => link.GetString()));
        JsonElement preferences = root.GetProperty("preferences");
        Assert.Equal(50, preferences.GetProperty("maxDistanceKm").GetInt32());
        Assert.Equal(1400m, preferences.GetProperty("minSalaryExpectation").GetDecimal());
        Assert.Equal(
            ["HYBRID", "REMOTE"],
            preferences.GetProperty("workModes").EnumerateArray().Select(mode => mode.GetString()));
        Assert.Equal("PERMANENT", Assert.Single(preferences.GetProperty("contractTypes").EnumerateArray()).GetString());
    }

    /// <summary>
    /// A gravação substitui os quatro campos, retira os espaços das extremidades e grava a nova localidade.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateSavesTheFourFieldsAndTheNewLocation()
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        Location other = await CandidateProfileTestData.SeedLocationAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            string.Empty,
            token,
            Json($$"""{"desiredRole":"  Programadora  ","locationId":"{{other.Id}}","availability":"IMMEDIATE","experienceSummary":"Resumo"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("Programadora", root.GetProperty("desiredRole").GetString());
        Assert.Equal("IMMEDIATE", root.GetProperty("availability").GetString());
        Assert.Equal($"{other.Name} (Porto)", root.GetProperty("locationName").GetString());
        Candidate saved = await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id);
        Assert.Equal(other.Id, saved.LocationId);
        Assert.Equal("Resumo", saved.ExperienceSummary);
    }

    /// <summary>
    /// <c>null</c> e texto só com espaços limpam os campos opcionais.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateWithNullsClearsTheOptionalFields()
    {
        (Candidate candidate, Location location, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory, c =>
        {
            c.DesiredRole = "Antigo";
            c.Availability = Availability.Immediate;
            c.ExperienceSummary = "Antigo";
        });

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            string.Empty,
            token,
            Json($$"""{"desiredRole":"   ","locationId":"{{location.Id}}","availability":null,"experienceSummary":null}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Candidate saved = await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id);
        Assert.Null(saved.DesiredRole);
        Assert.Null(saved.Availability);
        Assert.Null(saved.ExperienceSummary);
    }

    /// <summary>
    /// Os limites de 150 e 1000 caracteres são aceites e 151 e 1001 são recusados.
    /// </summary>
    /// <param name="roleLength">Comprimento da função pretendida.</param>
    /// <param name="summaryLength">Comprimento do resumo.</param>
    /// <param name="accepted">Se o pedido é aceite.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(150, 1000, true)]
    [InlineData(151, 1000, false)]
    [InlineData(150, 1001, false)]
    public async Task UpdateTextLengthsAreChecked(int roleLength, int summaryLength, bool accepted)
    {
        (_, Location location, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            string.Empty,
            token,
            Json($$"""{"desiredRole":"{{new string('a', roleLength)}}","locationId":"{{location.Id}}","experienceSummary":"{{new string('b', summaryLength)}}"}"""));

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Todos os erros de campo são indicados juntos, pela ordem do corpo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateReportsAllFieldErrorsTogether()
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            string.Empty,
            token,
            Json($$"""{"desiredRole":"{{new string('a', 151)}}","locationId":"{{Guid.NewGuid()}}","experienceSummary":"{{new string('b', 1001)}}"}"""));

        JsonElement error = await AssertValidationErrorAsync(response);
        Assert.Equal(
            [
                ("desiredRole", "O texto não pode exceder 150 caracteres."),
                ("locationId", "O valor não pertence à lista pré-definida."),
                ("experienceSummary", "O texto não pode exceder 1000 caracteres."),
            ],
            AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// Sem <c>locationId</c> o campo é obrigatório; uma disponibilidade fora da enumeração dá ERR-10 no campo.
    /// </summary>
    /// <param name="body">Corpo do pedido.</param>
    /// <param name="field">Campo em erro.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("{}", "locationId")]
    [InlineData("""{"availability":"QUANDO_QUISER"}""", "availability")]
    [InlineData("""{"availability":9}""", "availability")]
    public async Task UpdateWithMissingLocationOrInvalidAvailabilityReturnsErr10(string body, string field)
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, string.Empty, token, Json(body));

        JsonElement error = await AssertValidationErrorAsync(response);
        Assert.Contains(AuthTestAccounts.Fields(error), item => item.Field == field);
    }

    /// <summary>
    /// As preferências são gravadas e devolvidas; os valores repetidos das listas são retirados.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PreferencesAreSavedAndReturned()
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            "/preferences",
            token,
            Json("""{"maxDistanceKm":500,"minSalaryExpectation":0,"workModes":["REMOTE","REMOTE","HYBRID"],"contractTypes":["FIXED_TERM"]}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(500, root.GetProperty("maxDistanceKm").GetInt32());
        Assert.Equal(["REMOTE", "HYBRID"], root.GetProperty("workModes").EnumerateArray().Select(mode => mode.GetString()));
        Candidate saved = await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id);
        Assert.Equal(0m, saved.MinSalaryExpectation);
        Assert.Equal([ContractType.FixedTerm], saved.PreferredContractTypes);
    }

    /// <summary>
    /// <c>null</c> e listas vazias significam sem filtro.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PreferencesWithNullsAndEmptyListsMeanNoFilter()
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory, c =>
        {
            c.MaxDistanceKm = 20;
            c.MinSalaryExpectation = 900m;
            c.PreferredWorkModes = [WorkMode.OnSite];
        });

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            "/preferences",
            token,
            Json("""{"maxDistanceKm":null,"minSalaryExpectation":null,"workModes":null}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Candidate saved = await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id);
        Assert.Null(saved.MaxDistanceKm);
        Assert.Null(saved.MinSalaryExpectation);
        Assert.Empty(saved.PreferredWorkModes);
        Assert.Empty(saved.PreferredContractTypes);
    }

    /// <summary>
    /// A distância e a pretensão salarial fora dos limites dão ERR-10 no campo e as preferências anteriores mantêm-se.
    /// </summary>
    /// <param name="body">Corpo do pedido.</param>
    /// <param name="field">Campo em erro.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("""{"maxDistanceKm":0}""", "maxDistanceKm", "A distância máxima tem de ser um número inteiro entre 1 e 500.")]
    [InlineData("""{"maxDistanceKm":501}""", "maxDistanceKm", "A distância máxima tem de ser um número inteiro entre 1 e 500.")]
    [InlineData("""{"maxDistanceKm":12.5}""", "maxDistanceKm", "A distância máxima tem de ser um número inteiro entre 1 e 500.")]
    [InlineData("""{"minSalaryExpectation":-1}""", "minSalaryExpectation", "O valor tem de ser um número positivo em euros.")]
    [InlineData("""{"workModes":["ASSIM_ASSIM"]}""", "workModes[0]", "O valor não é válido.")]
    public async Task InvalidPreferencesReturnErr10AndKeepThePreviousOnes(string body, string field, string reason)
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory, c => c.MaxDistanceKm = 30);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, "/preferences", token, Json(body));

        JsonElement error = await AssertValidationErrorAsync(response);
        Assert.Contains((field, reason), AuthTestAccounts.Fields(error));
        Assert.Equal(30, (await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id)).MaxDistanceKm);
    }

    /// <summary>
    /// A distância de 1 a 500 é aceite.
    /// </summary>
    /// <param name="distance">Distância em quilómetros.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public async Task BoundaryDistancesAreAccepted(int distance)
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, "/preferences", token, Json($$"""{"maxDistanceKm":{{distance}}}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// De 0 a 3 hiperligações são guardadas tal como indicadas e pela ordem.
    /// </summary>
    /// <param name="count">Número de hiperligações.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task LinksAreSavedAsGivenAndInOrder(int count)
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        string[] urls = [.. Enumerable.Range(1, count).Select(index => $"HTTPS://Exemplo.pt/Perfil/{index}?a=B")];

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, "/links", token, Json(JsonSerializer.Serialize(new { urls })));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(urls, root.GetProperty("links").EnumerateArray().Select(link => link.GetString()));
        Candidate saved = await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id);
        Assert.Equal(urls, saved.Links.OrderBy(link => link.Position).Select(link => link.Url));
    }

    /// <summary>
    /// A lista nova substitui a anterior e a lista vazia apaga todas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LinksReplaceThePreviousListAndAnEmptyListDeletesAll()
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(
            this.factory,
            c => c.SetLinks(["https://a.pt", "https://b.pt", "https://c.pt"]));

        using HttpResponseMessage replaced = await this.SendAsync(HttpMethod.Put, "/links", token, Json("""{"urls":["https://c.pt","https://novo.pt"]}"""));
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Candidate afterReplace = await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id);
        Assert.Equal(["https://c.pt", "https://novo.pt"], afterReplace.Links.OrderBy(link => link.Position).Select(link => link.Url));

        using HttpResponseMessage cleared = await this.SendAsync(HttpMethod.Put, "/links", token, Json("""{"urls":[]}"""));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Empty((await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id)).Links);
    }

    /// <summary>
    /// Os endereços inválidos dão ERR-10 no campo <c>urls[i]</c>, e mais de 3 no campo <c>urls</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InvalidLinksReturnErr10WithTheIndexedFields()
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        string tooLong = "https://" + new string('a', 493);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            "/links",
            token,
            Json($$"""{"urls":["ftp://exemplo.pt","https://ok.pt","https://ok.pt","{{tooLong}}"]}"""));

        JsonElement error = await AssertValidationErrorAsync(response);
        Assert.Equal(
            [
                ("urls", "São permitidas no máximo 3 hiperligações."),
                ("urls[0]", "O endereço tem de começar por http:// ou https://."),
                ("urls[2]", "O endereço está repetido."),
                ("urls[3]", "O texto não pode exceder 500 caracteres."),
            ],
            AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// O endereço com 500 caracteres é aceite e com 501 é recusado.
    /// </summary>
    /// <param name="length">Comprimento do endereço.</param>
    /// <param name="accepted">Se o pedido é aceite.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public async Task LinkLengthIsChecked(int length, bool accepted)
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        string url = "https://" + new string('a', length - "https://".Length);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, "/links", token, Json($$"""{"urls":["{{url}}"]}"""));

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A fotografia válida responde 204 com o URL no <c>Location</c>, com nome gerado pelo servidor, e a consulta devolve o URL.
    /// </summary>
    /// <param name="fileName">Nome enviado.</param>
    /// <param name="extension">Extensão com que o servidor guarda.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("a-minha-foto.PNG", ".png")]
    [InlineData("selfie.jpeg", ".jpg")]
    public async Task ValidPhotoReturns204WithLocationAndAServerGeneratedName(string fileName, string extension)
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        byte[] content = extension == ".png" ? CandidateProfileTestData.Png : CandidateProfileTestData.Jpeg;

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, "/photo", token, CandidateProfileTestData.Multipart(fileName, content));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        string location = Assert.Single(response.Headers.GetValues("Location"));
        Assert.StartsWith("/api/files/PROFILE_PHOTO/", location, StringComparison.Ordinal);
        Assert.EndsWith(extension, location, StringComparison.Ordinal);
        Assert.DoesNotContain("foto", location, StringComparison.OrdinalIgnoreCase);
        Candidate saved = await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id);
        Assert.StartsWith("profile_photo/", saved.ProfilePhotoPath, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(this.factory.FileStorageRoot, saved.ProfilePhotoPath!)));

        using HttpResponseMessage profile = await this.SendAsync(HttpMethod.Get, string.Empty, token);
        JsonElement root = await AuthTestAccounts.ReadJsonAsync(profile);
        Assert.Equal(location, root.GetProperty("photoUrl").GetString());
    }

    /// <summary>
    /// Substituir a fotografia elimina o ficheiro anterior do armazenamento.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReplacingThePhotoDeletesThePreviousFile()
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        using HttpResponseMessage first = await this.SendAsync(HttpMethod.Put, "/photo", token, CandidateProfileTestData.Multipart("a.png", CandidateProfileTestData.Png));
        string firstPath = (await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id)).ProfilePhotoPath!;

        using HttpResponseMessage second = await this.SendAsync(HttpMethod.Put, "/photo", token, CandidateProfileTestData.Multipart("b.jpg", CandidateProfileTestData.Jpeg));

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        string secondPath = (await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id)).ProfilePhotoPath!;
        Assert.NotEqual(firstPath, secondPath);
        Assert.False(File.Exists(Path.Combine(this.factory.FileStorageRoot, firstPath)));
        Assert.True(File.Exists(Path.Combine(this.factory.FileStorageRoot, secondPath)));
    }

    /// <summary>
    /// O PDF válido responde 204 sem <c>Location</c>, o perfil passa a ter <c>hasCv</c> e o Candidato obtém o seu CV.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ValidCvReturns204WithoutLocationAndTheOwnerGetsIt()
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage upload = await this.SendAsync(HttpMethod.Put, "/cv", token, CandidateProfileTestData.Multipart("cv.pdf", CandidateProfileTestData.Pdf));

        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
        Assert.False(upload.Headers.Contains("Location"));
        using HttpResponseMessage profile = await this.SendAsync(HttpMethod.Get, string.Empty, token);
        Assert.True((await AuthTestAccounts.ReadJsonAsync(profile)).GetProperty("hasCv").GetBoolean());

        using HttpResponseMessage cv = await this.SendAsync(HttpMethod.Get, "/cv", token);
        Assert.Equal(HttpStatusCode.OK, cv.StatusCode);
        Assert.Equal("application/pdf", cv.Content.Headers.ContentType?.MediaType);
        Assert.Equal(CandidateProfileTestData.Pdf, await cv.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Cada Candidato só obtém o seu CV e sem CV a resposta é 404 ERR-20.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EachCandidateGetsOnlyTheirOwnCvAndWithoutOneTheAnswerIsErr20()
    {
        (_, _, string owner) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        (_, _, string other) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        using HttpResponseMessage upload = await this.SendAsync(HttpMethod.Put, "/cv", owner, CandidateProfileTestData.Multipart("cv.pdf", CandidateProfileTestData.Pdf));

        using HttpResponseMessage otherCv = await this.SendAsync(HttpMethod.Get, "/cv", other);

        await AuthenticationTests.AssertErrorAsync(
            otherCv,
            HttpStatusCode.NotFound,
            "ERR-20",
            "O elemento pedido não existe ou não está disponível.");
    }

    /// <summary>
    /// Substituir o CV elimina o ficheiro anterior.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReplacingTheCvDeletesThePreviousFile()
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        using HttpResponseMessage first = await this.SendAsync(HttpMethod.Put, "/cv", token, CandidateProfileTestData.Multipart("a.pdf", CandidateProfileTestData.Pdf));
        string firstPath = (await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id)).CvPath!;

        using HttpResponseMessage second = await this.SendAsync(HttpMethod.Put, "/cv", token, CandidateProfileTestData.Multipart("b.pdf", CandidateProfileTestData.Pdf));

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.False(File.Exists(Path.Combine(this.factory.FileStorageRoot, firstPath)));
    }

    /// <summary>
    /// Os ficheiros que não cumprem o formato real (RNF016) dão ERR-11 com o motivo e nada fica guardado.
    /// </summary>
    /// <param name="route">Rota do envio.</param>
    /// <param name="fileName">Nome enviado.</param>
    /// <param name="kind">Conteúdo enviado: png, jpeg, pdf, text, gif ou docx.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("/photo", "foto.png", "text", "O ficheiro tem de estar no formato JPEG ou PNG.")]
    [InlineData("/photo", "foto.png", "pdf", "O ficheiro tem de estar no formato JPEG ou PNG.")]
    [InlineData("/photo", "foto.jpg", "png", "O ficheiro tem de estar no formato JPEG ou PNG.")]
    [InlineData("/photo", "foto.gif", "gif", "O ficheiro tem de estar no formato JPEG ou PNG.")]
    [InlineData("/photo", "foto.png", "gif", "O ficheiro tem de estar no formato JPEG ou PNG.")]
    [InlineData("/photo", "foto.docx", "docx", "O ficheiro tem de estar no formato JPEG ou PNG.")]
    [InlineData("/photo", "foto", "png", "O ficheiro tem de estar no formato JPEG ou PNG.")]
    [InlineData("/cv", "cv.pdf", "png", "O ficheiro tem de estar no formato PDF.")]
    [InlineData("/cv", "cv.pdf", "text", "O ficheiro tem de estar no formato PDF.")]
    [InlineData("/cv", "cv.docx", "docx", "O ficheiro tem de estar no formato PDF.")]
    [InlineData("/cv", "cv.png", "pdf", "O ficheiro tem de estar no formato PDF.")]
    public async Task FilesWithTheWrongRealFormatReturnErr11(string route, string fileName, string kind, string reason)
    {
        (Candidate candidate, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, route, token, CandidateProfileTestData.Multipart(fileName, Content(kind)));

        await AssertFileRejectedAsync(response, reason);
        Candidate saved = await CandidateProfileTestData.ReadCandidateAsync(this.factory, candidate.Id);
        Assert.Null(saved.ProfilePhotoPath);
        Assert.Null(saved.CvPath);
    }

    /// <summary>
    /// 5 MB exatos são aceites; mais de 5 MB e até 6 MB dão ERR-11 com o motivo da dimensão.
    /// </summary>
    /// <param name="route">Rota do envio.</param>
    /// <param name="fileName">Nome enviado.</param>
    /// <param name="length">Dimensão do ficheiro.</param>
    /// <param name="accepted">Se o ficheiro é aceite.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("/photo", "foto.png", 5 * CandidateProfileTestData.Megabyte, true)]
    [InlineData("/photo", "foto.png", (5 * CandidateProfileTestData.Megabyte) + 1, false)]
    [InlineData("/photo", "foto.png", 6 * CandidateProfileTestData.Megabyte, false)]
    [InlineData("/cv", "cv.pdf", 5 * CandidateProfileTestData.Megabyte, true)]
    [InlineData("/cv", "cv.pdf", (5 * CandidateProfileTestData.Megabyte) + 1, false)]
    public async Task FileSizeLimitIs5MbWithErr11AboveIt(string route, string fileName, int length, bool accepted)
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        byte[] signature = route == "/cv" ? CandidateProfileTestData.Pdf : CandidateProfileTestData.Png;

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, route, token, CandidateProfileTestData.Multipart(fileName, CandidateProfileTestData.WithLength(signature, length)));

        if (accepted)
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        else
        {
            await AssertFileRejectedAsync(response, "O ficheiro excede 5 MB.");
        }
    }

    /// <summary>
    /// Um pedido acima de 6 MB responde 413 ERR-12. O limite é verificado pelo controller (Content-Length), porque o
    /// servidor de teste não aplica o limite do Kestrel.
    /// </summary>
    /// <param name="route">Rota do envio.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("/photo")]
    [InlineData("/cv")]
    public async Task RequestAbove6MbReturnsErr12(string route)
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);
        byte[] content = CandidateProfileTestData.WithLength(route == "/cv" ? CandidateProfileTestData.Pdf : CandidateProfileTestData.Png, (6 * CandidateProfileTestData.Megabyte) + 8192);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, route, token, CandidateProfileTestData.Multipart("grande.bin", content));

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.RequestEntityTooLarge,
            "ERR-12",
            "O pedido excede a dimensão máxima permitida.");
    }

    /// <summary>
    /// Um tipo de conteúdo que não é <c>multipart/form-data</c> responde 415 ERR-13.
    /// </summary>
    /// <param name="route">Rota do envio.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("/photo")]
    [InlineData("/cv")]
    public async Task NonMultipartRequestReturnsErr13(string route)
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Put, route, token, Json("{}"));

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.UnsupportedMediaType,
            "ERR-13",
            "O tipo de conteúdo do pedido não é suportado.");
    }

    /// <summary>
    /// Sem o campo <c>file</c>, ou com um ficheiro vazio, a resposta é ERR-11 com «O campo é obrigatório.».
    /// </summary>
    /// <param name="route">Rota do envio.</param>
    /// <param name="fieldName">Nome do campo enviado.</param>
    /// <param name="length">Dimensão do ficheiro enviado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("/photo", "outro", 10)]
    [InlineData("/photo", "file", 0)]
    [InlineData("/cv", "outro", 10)]
    [InlineData("/cv", "file", 0)]
    public async Task MissingOrEmptyFileReturnsErr11(string route, string fieldName, int length)
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Put,
            route,
            token,
            CandidateProfileTestData.Multipart("foto.png", new byte[length], fieldName));

        await AssertFileRejectedAsync(response, "O campo é obrigatório.");
    }

    /// <summary>
    /// Sem CV o pedido de obtenção responde 404 ERR-20.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetCvWithoutCvReturnsErr20()
    {
        (_, _, string token) = await CandidateProfileTestData.SeedCandidateAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "/cv", token);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.NotFound,
            "ERR-20",
            "O elemento pedido não existe ou não está disponível.");
    }

    /// <summary>
    /// Os sete endpoints respondem ERR-06 a Recrutadores e Administradores.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="method">Método HTTP.</param>
    /// <param name="relativePath">Caminho depois de <c>/api/candidate/profile</c>.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Recruiter, "GET", "")]
    [InlineData(UserType.Recruiter, "PUT", "")]
    [InlineData(UserType.Recruiter, "PUT", "/preferences")]
    [InlineData(UserType.Recruiter, "PUT", "/links")]
    [InlineData(UserType.Recruiter, "PUT", "/photo")]
    [InlineData(UserType.Recruiter, "PUT", "/cv")]
    [InlineData(UserType.Recruiter, "GET", "/cv")]
    [InlineData(UserType.Admin, "GET", "")]
    [InlineData(UserType.Admin, "PUT", "")]
    [InlineData(UserType.Admin, "PUT", "/preferences")]
    [InlineData(UserType.Admin, "PUT", "/links")]
    [InlineData(UserType.Admin, "PUT", "/photo")]
    [InlineData(UserType.Admin, "PUT", "/cv")]
    [InlineData(UserType.Admin, "GET", "/cv")]
    public async Task OtherAccountTypesReturnErr06(UserType userType, string method, string relativePath)
    {
        (_, string token) = await CandidateProfileTestData.SeedAccountAsync(this.factory, userType);

        using HttpResponseMessage response = await this.SendAsync(new HttpMethod(method), relativePath, token, Json("{}"));

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-06", Err06Message);
    }

    /// <summary>
    /// Sem credencial os sete endpoints respondem ERR-01.
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="relativePath">Caminho depois de <c>/api/candidate/profile</c>.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("GET", "")]
    [InlineData("PUT", "")]
    [InlineData("PUT", "/preferences")]
    [InlineData("PUT", "/links")]
    [InlineData("PUT", "/photo")]
    [InlineData("PUT", "/cv")]
    [InlineData("GET", "/cv")]
    public async Task MissingTokenReturnsErr01(string method, string relativePath)
    {
        using HttpResponseMessage response = await this.SendAsync(new HttpMethod(method), relativePath, token: null, Json("{}"));

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSessionMessage);
    }

    private static StringContent Json(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static byte[] Content(string kind)
    {
        return kind switch
        {
            "png" => CandidateProfileTestData.Png,
            "jpeg" => CandidateProfileTestData.Jpeg,
            "pdf" => CandidateProfileTestData.Pdf,
            "gif" => "GIF89a"u8.ToArray(),
            "docx" => [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00],
            _ => "Isto é só texto."u8.ToArray(),
        };
    }

    private static async Task<JsonElement> AssertValidationErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-10", error.GetProperty("code").GetString());
        Assert.Equal(ValidationMessage, error.GetProperty("message").GetString());
        return error;
    }

    private static async Task AssertFileRejectedAsync(HttpResponseMessage response, string reason)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-11", error.GetProperty("code").GetString());
        Assert.Equal(FileRejectedMessage, error.GetProperty("message").GetString());
        Assert.Equal([("file", reason)], AuthTestAccounts.Fields(error));
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, string? token, HttpContent? content = null)
    {
        return CandidateProfileTestData.SendAsync(this.factory, method, ProfileRoute + relativePath, token, content);
    }
}
