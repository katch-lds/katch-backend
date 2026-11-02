// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes da consulta das Empresas pendentes e dos dados submetidos, da aprovação e da recusa do registo (documentação
/// da API v02, secções 6.11.1 a 6.11.4; RF039, RF044, RF091 a RF094, RF104, RF112), sobre SQLite in-memory.
/// </summary>
public sealed class AdminCompanyRegistrationTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/admin/companies";
    private const string AnyId = "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a14";
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";
    private const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";
    private const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";
    private const string ValidReason = "A morada não corresponde à certidão.";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminCompanyRegistrationTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public AdminCompanyRegistrationTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// Gets the four operations of this Issue: method and route.
    /// </summary>
    public static TheoryData<string, string> Endpoints => new()
    {
        { "GET", $"{Route}/pending" },
        { "GET", $"{Route}/{AnyId}/registration" },
        { "POST", $"{Route}/{AnyId}/approve" },
        { "POST", $"{Route}/{AnyId}/reject" },
    };

    /// <summary>
    /// A aprovação dá <c>204</c>, passa a Empresa a aprovada, cumpre a política <c>ApprovedCompany</c> para o Recrutador e
    /// regista <c>COMPANY_APPROVED</c> com o Administrador (RF093, RF039, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ApproveApprovesTheCompanyAndOpensTheReservedOperations()
    {
        (AppUser admin, string adminToken) = await CompanyRegistrationTestData.SeedAdminAsync(this.factory);
        (Company company, _, _, string recruiterToken) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Pending);
        using (HttpResponseMessage before = await this.SendAsync(HttpMethod.Get, "/test-authorization/approved-company", recruiterToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        }

        using (HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{Route}/{company.Id}/approve", adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        using (HttpResponseMessage after = await this.SendAsync(HttpMethod.Get, "/test-authorization/approved-company", recruiterToken))
        {
            Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        }

        JsonElement status = await this.ReadStatusAsync(recruiterToken);
        Assert.Equal("APPROVED", status.GetProperty("status").GetString());
        Assert.Equal([(admin.Id, OperationType.CompanyApproved)], await CompanyRegistrationTestData.ReadOperationsAsync(this.factory, company.Id));
    }

    /// <summary>
    /// A recusa dá <c>204</c>, o Recrutador vê o motivo aparado no estado do pedido (6.4.3) e fica registado
    /// <c>COMPANY_REJECTED</c> com o Administrador (RF094, RF044, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectRejectsTheCompanyAndTheRecruiterSeesTheReason()
    {
        (AppUser admin, string adminToken) = await CompanyRegistrationTestData.SeedAdminAsync(this.factory);
        (Company company, _, _, string recruiterToken) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Pending);

        using (HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Post,
            $"{Route}/{company.Id}/reject",
            adminToken,
            new { reason = $"  {ValidReason}  " }))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        JsonElement status = await this.ReadStatusAsync(recruiterToken);
        Assert.Equal("REJECTED", status.GetProperty("status").GetString());
        Assert.Equal(ValidReason, status.GetProperty("rejectionReason").GetString());
        Assert.Equal([(admin.Id, OperationType.CompanyRejected)], await CompanyRegistrationTestData.ReadOperationsAsync(this.factory, company.Id));
    }

    /// <summary>
    /// Um motivo em falta, vazio, curto ou longo dá <c>400</c> (ERR-10) com o campo <c>reason</c>, e a Empresa continua
    /// pendente e sem registo.
    /// </summary>
    /// <param name="reason">Motivo enviado, ou <see langword="null"/> para um corpo sem motivo.</param>
    /// <param name="expectedReason">Motivo do erro de campo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null, "O campo é obrigatório.")]
    [InlineData("   ", "O campo é obrigatório.")]
    [InlineData("curto", "O motivo tem de ter entre 10 e 500 caracteres.")]
    public async Task RejectWithAnInvalidReasonReturnsErr10(string? reason, string expectedReason)
    {
        (_, string adminToken) = await CompanyRegistrationTestData.SeedAdminAsync(this.factory);
        (Company company, _, _, string recruiterToken) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Pending);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Post,
            $"{Route}/{company.Id}/reject",
            adminToken,
            new { reason });

        JsonElement error = await CompanyRegistrationTestData.AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "ERR-10",
            "Os dados enviados não cumprem a validação automática.");
        Assert.Equal([("reason", expectedReason)], AuthTestAccounts.Fields(error));
        Assert.Equal("PENDING", (await this.ReadStatusAsync(recruiterToken)).GetProperty("status").GetString());
        Assert.Empty(await CompanyRegistrationTestData.ReadOperationsAsync(this.factory, company.Id));
    }

    /// <summary>
    /// Um motivo com mais de 500 caracteres dá <c>400</c> (ERR-10) no campo <c>reason</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectWithATooLongReasonReturnsErr10()
    {
        (_, string adminToken) = await CompanyRegistrationTestData.SeedAdminAsync(this.factory);
        (Company company, _, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Pending);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Post,
            $"{Route}/{company.Id}/reject",
            adminToken,
            new { reason = new string('x', 501) });

        JsonElement error = await CompanyRegistrationTestData.AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "ERR-10",
            "Os dados enviados não cumprem a validação automática.");
        Assert.Equal([("reason", "O motivo tem de ter entre 10 e 500 caracteres.")], AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// Um pedido de recusa sem corpo dá <c>400</c> (ERR-10) no campo <c>reason</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectWithoutABodyReturnsErr10()
    {
        (_, string adminToken) = await CompanyRegistrationTestData.SeedAdminAsync(this.factory);
        (Company company, _, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Pending);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{Route}/{company.Id}/reject", adminToken);

        JsonElement error = await CompanyRegistrationTestData.AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "ERR-10",
            "Os dados enviados não cumprem a validação automática.");
        Assert.Equal([("reason", "O campo é obrigatório.")], AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// Uma Empresa inexistente dá <c>404</c> (ERR-20) nas operações sobre uma Empresa.
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="operation">Fim da rota.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("GET", "registration")]
    [InlineData("POST", "approve")]
    [InlineData("POST", "reject")]
    public async Task UnknownCompanyReturns404(string method, string operation)
    {
        (_, string adminToken) = await CompanyRegistrationTestData.SeedAdminAsync(this.factory);

        using HttpResponseMessage response = await this.SendAsync(
            new HttpMethod(method),
            $"{Route}/{Guid.NewGuid()}/{operation}",
            adminToken,
            operation == "reject" ? new { reason = ValidReason } : null);

        await CompanyRegistrationTestData.AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
    }

    /// <summary>
    /// Aprovar ou recusar uma Empresa que não está pendente dá <c>409</c> (ERR-30) com o estado atual e não fica
    /// registado, incluindo repetir a aprovação.
    /// </summary>
    /// <param name="status">Estado atual da Empresa.</param>
    /// <param name="operation">Operação pedida.</param>
    /// <param name="label">Designação do estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Approved, "approve", "aprovada")]
    [InlineData(CompanyStatus.Rejected, "approve", "recusada")]
    [InlineData(CompanyStatus.Suspended, "approve", "suspensa")]
    [InlineData(CompanyStatus.Approved, "reject", "aprovada")]
    [InlineData(CompanyStatus.Rejected, "reject", "recusada")]
    [InlineData(CompanyStatus.Suspended, "reject", "suspensa")]
    public async Task DecidingACompanyThatIsNotPendingReturns409WithTheCurrentState(CompanyStatus status, string operation, string label)
    {
        (_, string adminToken) = await CompanyRegistrationTestData.SeedAdminAsync(this.factory);
        (Company company, _, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, status);

        using HttpResponseMessage response = await this.SendAsync(
            HttpMethod.Post,
            $"{Route}/{company.Id}/{operation}",
            adminToken,
            operation == "reject" ? new { reason = ValidReason } : null);

        await CompanyRegistrationTestData.AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "ERR-30",
            $"A operação não é permitida no estado atual: {label}.");
        Assert.Empty(await CompanyRegistrationTestData.ReadOperationsAsync(this.factory, company.Id));
    }

    /// <summary>
    /// Repetir a aprovação da mesma Empresa dá <c>204</c> e depois <c>409</c> (ERR-30), com um único registo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RepeatingTheApprovalReturns409()
    {
        (_, string adminToken) = await CompanyRegistrationTestData.SeedAdminAsync(this.factory);
        (Company company, _, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Pending);

        using (HttpResponseMessage first = await this.SendAsync(HttpMethod.Post, $"{Route}/{company.Id}/approve", adminToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        }

        using HttpResponseMessage second = await this.SendAsync(HttpMethod.Post, $"{Route}/{company.Id}/approve", adminToken);
        await CompanyRegistrationTestData.AssertErrorAsync(
            second,
            HttpStatusCode.Conflict,
            "ERR-30",
            "A operação não é permitida no estado atual: aprovada.");
        Assert.Single(await CompanyRegistrationTestData.ReadOperationsAsync(this.factory, company.Id));
    }

    /// <summary>
    /// A lista de pendentes tem os quatro campos, da submissão mais antiga para a mais recente, e não inclui as Empresas
    /// que não estão pendentes (6.11.1; RF091).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListPendingReturnsOnlyPendingCompaniesFromTheOldestSubmission()
    {
        (_, string adminToken) = await CompanyRegistrationTestData.SeedAdminAsync(this.factory);
        var oldest = new DateTimeOffset(2020, 3, 1, 9, 0, 0, TimeSpan.Zero);
        (Company newer, _, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Pending, "Zeta, Lda.", oldest.AddDays(10));
        (Company older, _, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Pending, "Lumen, Lda.", oldest);
        (Company approved, _, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved, submittedAt: oldest.AddDays(-1));
        (Company rejected, _, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Rejected, submittedAt: oldest.AddDays(-2));
        (Company suspended, _, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Suspended, submittedAt: oldest.AddDays(-3));

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, $"{Route}/pending", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<JsonElement> list = (await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().ToList();
        List<Guid> ids = list.Select(item => item.GetProperty("companyId").GetGuid()).ToList();
        Assert.Equal([older.Id, newer.Id], ids.Where(id => id == older.Id || id == newer.Id));
        Assert.DoesNotContain(ids, id => id == approved.Id || id == rejected.Id || id == suspended.Id);
        JsonElement first = list.Single(item => item.GetProperty("companyId").GetGuid() == older.Id);
        Assert.Equal(
            ["companyId", "companyName", "submittedAt", "taxId"],
            first.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Lumen, Lda.", first.GetProperty("companyName").GetString());
        Assert.Equal(older.TaxId, first.GetProperty("taxId").GetString());
        Assert.Equal(oldest, first.GetProperty("submittedAt").GetDateTimeOffset());
        List<DateTimeOffset> dates = list.Select(item => item.GetProperty("submittedAt").GetDateTimeOffset()).ToList();
        Assert.Equal(dates.Order(), dates);
    }

    /// <summary>
    /// Os dados submetidos têm os onze campos, com a localidade no formato «Localidade (Distrito)» e o setor
    /// serializado como no resto da API, em qualquer estado da Empresa (6.11.2; RF092).
    /// </summary>
    /// <param name="status">Estado da Empresa.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending)]
    [InlineData(CompanyStatus.Rejected)]
    public async Task GetSubmittedDataReturnsTheElevenFields(CompanyStatus status)
    {
        (_, string adminToken) = await CompanyRegistrationTestData.SeedAdminAsync(this.factory);
        (Company company, Location location, _, _) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, status);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, $"{Route}/{company.Id}/registration", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement data = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(
            [
                "address",
                "companyId",
                "companyName",
                "contactEmail",
                "contactPhone",
                "industry",
                "locationId",
                "locationName",
                "responsibleName",
                "submittedAt",
                "taxId",
            ],
            data.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(company.Id, data.GetProperty("companyId").GetGuid());
        Assert.Equal("Lumen Software, Lda.", data.GetProperty("companyName").GetString());
        Assert.Equal(company.TaxId, data.GetProperty("taxId").GetString());
        Assert.Equal("TECHNOLOGY", data.GetProperty("industry").GetString());
        Assert.Equal("Avenida da Liberdade, 100, 4710-000 Braga", data.GetProperty("address").GetString());
        Assert.Equal(location.Id, data.GetProperty("locationId").GetGuid());
        Assert.Equal($"{location.Name} (Braga)", data.GetProperty("locationName").GetString());
        Assert.Equal("geral@lumen-exemplo.pt", data.GetProperty("contactEmail").GetString());
        Assert.Equal("253000111", data.GetProperty("contactPhone").GetString());
        Assert.Equal("Rui Matos", data.GetProperty("responsibleName").GetString());
        Assert.Equal(company.SubmittedAt, data.GetProperty("submittedAt").GetDateTimeOffset());
    }

    /// <summary>
    /// O Candidato e o Recrutador recebem <c>403</c> (ERR-06) em todas as operações (RF104).
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="path">Rota.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task OtherAccountTypesReturnErr06(string method, string path)
    {
        string candidateToken = await CompanyRegistrationTestData.SeedCandidateTokenAsync(this.factory);
        (_, _, _, string recruiterToken) = await CompanyRegistrationTestData.SeedCompanyAsync(this.factory, CompanyStatus.Approved);

        foreach (string token in new[] { candidateToken, recruiterToken })
        {
            using HttpResponseMessage response = await this.SendAsync(new HttpMethod(method), path, token);
            await CompanyRegistrationTestData.AssertErrorAsync(
                response,
                HttpStatusCode.Forbidden,
                "ERR-06",
                Err06Message);
        }
    }

    /// <summary>
    /// Sem credencial, todas as operações dão <c>401</c> (ERR-01).
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="path">Rota.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EndpointsRequireASession(string method, string path)
    {
        using HttpResponseMessage response = await this.SendAsync(new HttpMethod(method), path, token: null);
        await CompanyRegistrationTestData.AssertErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            "ERR-01",
            InvalidSessionMessage);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null)
    {
        return CompanyRegistrationTestData.SendAsync(this.factory, method, path, token, body);
    }

    private async Task<JsonElement> ReadStatusAsync(string recruiterToken)
    {
        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "/api/recruiter/company/status", recruiterToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await AuthTestAccounts.ReadJsonAsync(response);
    }
}
