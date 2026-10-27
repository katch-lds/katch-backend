// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de <c>POST /api/recruiter/company</c>, <c>POST /api/recruiter/company/resubmission</c> e
/// <c>GET /api/recruiter/company/status</c> sobre a API em memória (documentação da API v02, secções 6.4.1 a 6.4.3;
/// RF039, RF041 a RF045, RF112).
/// </summary>
public sealed class CompanyRegistrationTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/recruiter/company";
    private const string ResubmissionRoute = "/api/recruiter/company/resubmission";
    private const string StatusRoute = "/api/recruiter/company/status";
    private const string RejectionReason = "A morada indicada não corresponde à certidão permanente.";

    private static long taxIdCounter = 509_000_000;

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyRegistrationTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public CompanyRegistrationTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Com dados válidos: 201 com <c>Location</c> do estado, Empresa pendente com os dados enviados e a submissão no
    /// <c>operation_log</c> com o Recrutador como autor (RF041, RF043, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ValidRegistrationReturns201PendingAndLogsTheSubmission()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string token, Guid recruiterId) = await CreateRecruiterAsync(client);
        string taxId = NextTaxId();

        using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, Route, token, Body(taxId, location.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(StatusRoute, response.Headers.Location?.OriginalString);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement root = json.RootElement;
        Assert.Equal("PENDING", root.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("rejectionReason").ValueKind);
        Assert.EndsWith("Z", root.GetProperty("submittedAt").GetString(), StringComparison.Ordinal);
        Guid companyId = root.GetProperty("companyId").GetGuid();

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        Company company = await context.Companies.AsNoTracking().SingleAsync(c => c.Id == companyId, cancellationToken);
        Assert.Equal(recruiterId, company.RecruiterId);
        Assert.Equal(CompanyStatus.Pending, company.Status);
        Assert.Equal(taxId, company.TaxId);
        Assert.Equal(Industry.Technology, company.Industry);
        Assert.Equal(location.Id, company.LocationId);
        Assert.Equal("Rui Matos", company.ResponsibleName);
        OperationLog entry = Assert.Single(
            await context.OperationLogs.AsNoTracking().Where(log => log.EntityId == companyId).ToListAsync(cancellationToken));
        Assert.Equal(OperationType.CompanySubmitted, entry.Operation);
        Assert.Equal(recruiterId, entry.UserId);
    }

    /// <summary>
    /// Com todos os campos inválidos, o ERR-10 indica os oito campos no mesmo pedido, e nada é gravado (RF042).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AllInvalidFieldsAreReportedInTheSameResponse()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string token, Guid recruiterId) = await CreateRecruiterAsync(client);
        var body = new
        {
            companyName = " ",
            taxId = "50912345",
            industry = (string?)null,
            address = string.Empty,
            locationId = Guid.NewGuid(),
            contactEmail = "geral.lumen-exemplo.pt",
            contactPhone = "25300011",
            responsibleName = " ",
        };

        using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, Route, token, body);

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10");
        Assert.Equal(
            [
                ("companyName", "O campo é obrigatório."),
                ("taxId", "O número de identificação fiscal tem de ter 9 algarismos."),
                ("industry", "O campo é obrigatório."),
                ("address", "O campo é obrigatório."),
                ("locationId", "O valor não pertence à lista pré-definida."),
                ("contactEmail", "O endereço de correio eletrónico tem de ter o formato local@domínio."),
                ("contactPhone", "O contacto telefónico tem de ter 9 algarismos."),
                ("responsibleName", "O campo é obrigatório."),
            ],
            Fields(error));
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        Assert.False(await context.Companies.AnyAsync(c => c.RecruiterId == recruiterId, cancellationToken));
    }

    /// <summary>
    /// Sem corpo, os oito campos são indicados como obrigatórios.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmptyBodyReportsEveryFieldAsRequired()
    {
        await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string token, _) = await CreateRecruiterAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new("Bearer", token);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10");
        Assert.Equal(
            ["companyName", "taxId", "industry", "address", "locationId", "contactEmail", "contactPhone", "responsibleName"],
            Fields(error).Select(field => field.Field));
    }

    /// <summary>
    /// Um setor fora da lista (P15) é indicado no campo <c>industry</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task IndustryOutsideTheListReturnsErr10ForThatField()
    {
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string token, _) = await CreateRecruiterAsync(client);
        string json = JsonSerializer.Serialize(Body(NextTaxId(), location.Id)).Replace("TECHNOLOGY", "AGRICULTURE", StringComparison.Ordinal);
        using var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new("Bearer", token);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10");
        Assert.Equal([("industry", "O valor não pertence à lista pré-definida.")], Fields(error));
    }

    /// <summary>
    /// O NIF de outra Empresa é recusado com o ERR-10 do campo <c>taxId</c> (RF042).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DuplicateTaxIdReturnsErr10()
    {
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string first, _) = await CreateRecruiterAsync(client);
        (string second, _) = await CreateRecruiterAsync(client);
        string taxId = NextTaxId();
        using HttpResponseMessage registered = await SendAsync(client, HttpMethod.Post, Route, first, Body(taxId, location.Id));
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, Route, second, Body(taxId, location.Id));

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10");
        Assert.Equal([("taxId", "O número de identificação fiscal já está registado.")], Fields(error));
    }

    /// <summary>
    /// Um segundo registo do mesmo Recrutador devolve 409 (ERR-40) e não cria outra Empresa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SecondRegistrationOfTheSameRecruiterReturnsErr40()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string token, Guid recruiterId) = await CreateRecruiterAsync(client);
        using HttpResponseMessage first = await SendAsync(client, HttpMethod.Post, Route, token, Body(NextTaxId(), location.Id));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage second = await SendAsync(client, HttpMethod.Post, Route, token, Body(NextTaxId(), location.Id));

        JsonElement error = await AssertErrorAsync(second, HttpStatusCode.Conflict, "ERR-40");
        Assert.Equal("O Recrutador já registou uma Empresa.", error.GetProperty("message").GetString());
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        Assert.Equal(1, await context.Companies.CountAsync(c => c.RecruiterId == recruiterId, cancellationToken));
    }

    /// <summary>
    /// A nova submissão de um pedido recusado devolve 200 com a Empresa pendente, sem motivo e com a data de submissão
    /// atualizada, grava os dados corrigidos e regista outra submissão no <c>operation_log</c> (RF045, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ResubmissionOfARejectedCompanyReturnsToPending()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string token, _) = await CreateRecruiterAsync(client);
        string taxId = NextTaxId();
        Guid companyId = await RegisterAsync(client, token, taxId, location.Id);
        await this.ChangeStatusAsync(companyId, company => company.Reject(RejectionReason));
        DateTimeOffset before = await this.SubmittedAtAsync(companyId);
        DateTimeOffset resubmittedAt = WholeSeconds(before.AddHours(1));

        object corrected = Body(taxId, location.Id, address: "Avenida da Liberdade, 102, 4710-000 Braga");
        using HttpResponseMessage response = await this.AtAsync(
            resubmittedAt,
            () => SendAsync(client, HttpMethod.Post, ResubmissionRoute, token, corrected));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("PENDING", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("rejectionReason").ValueKind);
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        Company company = await context.Companies.AsNoTracking().SingleAsync(c => c.Id == companyId, cancellationToken);
        Assert.Equal(CompanyStatus.Pending, company.Status);
        Assert.Null(company.StatusReason);
        Assert.Equal(resubmittedAt, company.SubmittedAt);
        Assert.Equal("Avenida da Liberdade, 102, 4710-000 Braga", company.Address);
        List<OperationType> operations = await context.OperationLogs.AsNoTracking()
            .Where(log => log.EntityId == companyId)
            .Select(log => log.Operation)
            .ToListAsync(cancellationToken);
        Assert.Equal([OperationType.CompanySubmitted, OperationType.CompanySubmitted], operations);
    }

    /// <summary>
    /// Fora do estado recusado, a nova submissão devolve 409 (ERR-30) com o estado atual e nada muda.
    /// </summary>
    /// <param name="status">Estado da Empresa.</param>
    /// <param name="label">Estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending, "pendente")]
    [InlineData(CompanyStatus.Approved, "aprovada")]
    [InlineData(CompanyStatus.Suspended, "suspensa")]
    public async Task ResubmissionOutsideTheRejectedStateReturnsErr30(CompanyStatus status, string label)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string token, _) = await CreateRecruiterAsync(client);
        string taxId = NextTaxId();
        Guid companyId = await RegisterAsync(client, token, taxId, location.Id);
        await this.ChangeStatusAsync(companyId, company => MoveTo(company, status));

        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            ResubmissionRoute,
            token,
            Body(taxId, location.Id));

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.Conflict, "ERR-30");
        Assert.Equal($"A operação não é permitida no estado atual: {label}.", error.GetProperty("message").GetString());
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        Assert.Equal(status, (await context.Companies.AsNoTracking().SingleAsync(c => c.Id == companyId, cancellationToken)).Status);
        Assert.Equal(1, await context.OperationLogs.CountAsync(log => log.EntityId == companyId, cancellationToken));
    }

    /// <summary>
    /// Sem Empresa registada, a nova submissão e a consulta do estado devolvem 404 (ERR-20).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ResubmissionAndStatusWithoutACompanyReturnErr20()
    {
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string token, _) = await CreateRecruiterAsync(client);

        using HttpResponseMessage resubmission = await SendAsync(
            client,
            HttpMethod.Post,
            ResubmissionRoute,
            token,
            Body(NextTaxId(), location.Id));
        using HttpResponseMessage status = await SendAsync(client, HttpMethod.Get, StatusRoute, token, body: null);

        foreach (HttpResponseMessage response in new[] { resubmission, status })
        {
            JsonElement error = await AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20");
            Assert.Equal("O elemento pedido não existe ou não está disponível.", error.GetProperty("message").GetString());
        }
    }

    /// <summary>
    /// A consulta do estado devolve o estado atual, com o motivo só no estado recusado (RF044).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task StatusReturnsTheReasonOnlyWhenRejected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string token, _) = await CreateRecruiterAsync(client);
        Guid companyId = await RegisterAsync(client, token, NextTaxId(), location.Id);

        using HttpResponseMessage pending = await SendAsync(client, HttpMethod.Get, StatusRoute, token, body: null);
        await this.ChangeStatusAsync(companyId, company => company.Reject(RejectionReason));
        using HttpResponseMessage rejected = await SendAsync(client, HttpMethod.Get, StatusRoute, token, body: null);

        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        using JsonDocument pendingJson = JsonDocument.Parse(await pending.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(companyId, pendingJson.RootElement.GetProperty("companyId").GetGuid());
        Assert.Equal("PENDING", pendingJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, pendingJson.RootElement.GetProperty("rejectionReason").ValueKind);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        using JsonDocument rejectedJson = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("REJECTED", rejectedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(RejectionReason, rejectedJson.RootElement.GetProperty("rejectionReason").GetString());
    }

    /// <summary>
    /// Antes da aprovação, as operações <c>ApprovedCompany</c> devolvem 403 (ERR-07) com o estado da Empresa; depois da
    /// aprovação, ficam disponíveis (RF039).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ApprovedCompanyOperationsReturnErr07UntilTheApproval()
    {
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        (string token, _) = await CreateRecruiterAsync(client);
        Guid companyId = await RegisterAsync(client, token, NextTaxId(), location.Id);

        using HttpResponseMessage before = await SendAsync(
            client,
            HttpMethod.Get,
            "/test-authorization/approved-company",
            token,
            body: null);
        await this.ChangeStatusAsync(companyId, company => company.Approve());
        using HttpResponseMessage after = await SendAsync(
            client,
            HttpMethod.Get,
            "/test-authorization/approved-company",
            token,
            body: null);

        JsonElement error = await AssertErrorAsync(before, HttpStatusCode.Forbidden, "ERR-07");
        Assert.Equal(
            "A operação exige a Empresa aprovada. Estado atual da Empresa: pendente.",
            error.GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    /// <summary>
    /// Os endpoints da Empresa exigem a política <c>Recruiter</c>: sem credencial devolvem 401 (ERR-01) e com a
    /// credencial de um Candidato devolvem 403 (ERR-06).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CompanyEndpointsRequireARecruiter()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        using HttpResponseMessage candidate = await client.PostAsJsonAsync(
            "/api/auth/candidates",
            new
            {
                fullName = "Ana Ferreira",
                email = $"candidata-{Guid.NewGuid():N}@exemplo.pt",
                phoneNumber = "912345678",
                locationId = location.Id,
                password = "katch2026",
                acceptTerms = true,
            },
            cancellationToken);
        using JsonDocument json = JsonDocument.Parse(await candidate.Content.ReadAsStringAsync(cancellationToken));
        string candidateToken = json.RootElement.GetProperty("token").GetString()!;

        using HttpResponseMessage anonymous = await SendAsync(client, HttpMethod.Get, StatusRoute, token: null, body: null);
        using HttpResponseMessage asCandidate = await SendAsync(
            client,
            HttpMethod.Post,
            Route,
            candidateToken,
            Body(NextTaxId(), location.Id));

        await AssertErrorAsync(anonymous, HttpStatusCode.Unauthorized, "ERR-01");
        await AssertErrorAsync(asCandidate, HttpStatusCode.Forbidden, "ERR-06");
    }

    private static string NextTaxId()
    {
        return Interlocked.Increment(ref taxIdCounter).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static object Body(string taxId, Guid locationId, string address = "Avenida da Liberdade, 100, 4710-000 Braga")
    {
        return new
        {
            companyName = "Lumen Software, Lda.",
            taxId,
            industry = "TECHNOLOGY",
            address,
            locationId,
            contactEmail = "geral@lumen-exemplo.pt",
            contactPhone = "253000111",
            responsibleName = "Rui Matos",
        };
    }

    private static DateTimeOffset WholeSeconds(DateTimeOffset instant)
    {
        return new DateTimeOffset(instant.UtcTicks - (instant.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }

    private static void MoveTo(Company company, CompanyStatus status)
    {
        if (status is CompanyStatus.Approved or CompanyStatus.Suspended)
        {
            company.Approve();
        }

        if (status == CompanyStatus.Suspended)
        {
            company.Suspend();
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string route,
        string? token,
        object? body)
    {
        using var request = new HttpRequestMessage(method, route);
        if (token is not null)
        {
            request.Headers.Authorization = new("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement root = json.RootElement.Clone();
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("blockedUntil").ValueKind);
        return root;
    }

    private static List<(string Field, string Reason)> Fields(JsonElement error)
    {
        return error.GetProperty("fields")
            .EnumerateArray()
            .Select(field => (field.GetProperty("field").GetString()!, field.GetProperty("reason").GetString()!))
            .ToList();
    }

    private static async Task<(string Token, Guid RecruiterId)> CreateRecruiterAsync(HttpClient client)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/recruiters",
            new { email = $"recrutador-{Guid.NewGuid():N}@exemplo.pt", password = "Lumen2026rh", acceptTerms = true },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return (json.RootElement.GetProperty("token").GetString()!, json.RootElement.GetProperty("userId").GetGuid());
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, string token, string taxId, Guid locationId)
    {
        using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, Route, token, Body(taxId, locationId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return json.RootElement.GetProperty("companyId").GetGuid();
    }

    private async Task ChangeStatusAsync(Guid companyId, Action<Company> change)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        Company company = await context.Companies.SingleAsync(c => c.Id == companyId, cancellationToken);
        change(company);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Envia um pedido com o relógio da API fixado no instante indicado, e repõe o relógio real no fim.
    /// </summary>
    private async Task<HttpResponseMessage> AtAsync(DateTimeOffset instant, Func<Task<HttpResponseMessage>> send)
    {
        this.factory.Clock.Fixed = instant;
        try
        {
            return await send();
        }
        finally
        {
            this.factory.Clock.Fixed = null;
        }
    }

    private async Task<DateTimeOffset> SubmittedAtAsync(Guid companyId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return (await context.Companies.AsNoTracking().SingleAsync(c => c.Id == companyId, TestContext.Current.CancellationToken))
            .SubmittedAt;
    }

    private async Task<Location> SeedLocationAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545m, -8.426m);

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        context.Locations.Add(location);
        await context.SaveChangesAsync(cancellationToken);
        return location;
    }
}
