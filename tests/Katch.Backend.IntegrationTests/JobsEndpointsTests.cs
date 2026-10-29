// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes dos endpoints de gestão das vagas (documentação da API v02, secção 6.5; RF050 a RF059, RF112 a RF115):
/// acesso, titularidade, validação da criação e da alteração, transições de estado, eliminação e registo de cada
/// operação no <c>operation_log</c>.
/// </summary>
public sealed class JobsEndpointsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/recruiter/jobs";

    private static long taxIdCounter = 800_000_000;

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobsEndpointsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação de teste partilhada.</param>
    public JobsEndpointsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Gets the invalid creation requests, with the expected field and reason (documentação da API v02, secções 4.3,
    /// 6.5.2 e 8.3). <c>{skill}</c> é substituído pelo identificador de uma competência da lista.
    /// </summary>
    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "minSalary", "0", "minSalary:O valor tem de ser um número positivo em euros." },
        { "maxSalary", "-10", "maxSalary:O valor tem de ser um número positivo em euros." },
        { "minSalary", "2200", "minSalary:O valor mínimo não pode ser superior ao valor máximo." },
        { "maxSalary", "100000000", "maxSalary:O valor não pode exceder 99 999 999,99 euros." },
        { "skillIds", "[]", "skillIds:O campo é obrigatório." },
        { "skillIds", "null", "skillIds:O campo é obrigatório." },
        { "skillIds", "[\"{skill}\", \"{skill}\"]", "skillIds[1]:O valor está repetido na lista." },
        { "skillIds", "[\"2f6c3a3e-0000-4000-8000-000000000001\"]", "skillIds[0]:O valor não pertence à lista pré-definida." },
        { "benefitIds", "[\"2f6c3a3e-0000-4000-8000-000000000002\"]", "benefitIds[0]:O valor não pertence à lista pré-definida." },
        { "locationId", "\"2f6c3a3e-0000-4000-8000-000000000003\"", "locationId:O valor não pertence à lista pré-definida." },
        { "contractType", "\"FULL_TIME\"", "contractType:O valor não pertence à lista pré-definida." },
        { "workMode", "null", "workMode:O campo é obrigatório." },
        { "title", "\"   \"", "title:O campo é obrigatório." },
        { "title", "\"" + new string('t', 151) + "\"", "title:O texto não pode exceder 150 caracteres." },
        { "expiresAt", "\"2020-01-01T00:00:00Z\"", "expiresAt:A data-limite tem de ser posterior ao momento atual." },
    };

    /// <summary>
    /// A criação devolve <c>201</c> com a vaga não publicada, sem <c>Location</c>, com as listas por ordem alfabética, e
    /// regista <c>JOB_CREATED</c> com o Recrutador (RF050, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CreateReturnsAnUnpublishedJobAndLogsIt()
    {
        Fixture fixture = await this.SeedCompanyAsync();

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, Route, fixture.Recruiter, fixture.ValidBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        JsonElement job = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("UNPUBLISHED", job.GetProperty("status").GetString());
        Assert.Equal("Programador Backend .NET", job.GetProperty("title").GetString());
        Assert.Equal(1600.00m, job.GetProperty("minSalary").GetDecimal());
        Assert.Equal($"{fixture.Location.Name} (Braga)", job.GetProperty("locationName").GetString());
        Assert.Equal("HYBRID", job.GetProperty("workMode").GetString());
        Assert.Equal([fixture.CSharp.Name, fixture.Sql.Name], job.GetProperty("skills").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.Equal([fixture.Health.Name], job.GetProperty("benefits").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.True(job.GetProperty("isUrgent").GetBoolean());
        Assert.Equal(JsonValueKind.Null, job.GetProperty("publishedAt").ValueKind);
        Assert.Equal(0, job.GetProperty("photoUrls").GetArrayLength());
        Assert.Equal(0, job.GetProperty("waitingCandidates").GetInt32());

        Guid jobId = job.GetProperty("jobId").GetGuid();
        Assert.Equal([(OperationType.JobCreated, fixture.Recruiter.Id)], await this.ReadLogAsync(jobId));
    }

    /// <summary>
    /// Sem corpo, todos os campos obrigatórios são indicados na mesma resposta (RF050).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CreateWithoutBodyListsEveryRequiredField()
    {
        Fixture fixture = await this.SeedCompanyAsync();

        using HttpResponseMessage response = await this.SendRawAsync(HttpMethod.Post, Route, fixture.Recruiter, string.Empty);

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10");
        Assert.Equal(
            ["title", "description", "minSalary", "maxSalary", "locationId", "contractType", "workMode", "skillIds"],
            AuthTestAccounts.Fields(error).Select(field => field.Field));
        Assert.All(AuthTestAccounts.Fields(error), field => Assert.Equal("O campo é obrigatório.", field.Reason));
    }

    /// <summary>
    /// Cada campo inválido é recusado com <c>400</c> (ERR-10), o campo e o motivo, e nada é gravado.
    /// </summary>
    /// <param name="field">Campo alterado no pedido válido.</param>
    /// <param name="json">Valor JSON do campo.</param>
    /// <param name="expected">Campo e motivo esperados, separados por <c>:</c>.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task CreateRejectsInvalidFields(string field, string json, string expected)
    {
        Fixture fixture = await this.SeedCompanyAsync();
        string body = fixture.ValidJson((field, json));

        using HttpResponseMessage response = await this.SendRawAsync(HttpMethod.Post, Route, fixture.Recruiter, body);

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10");
        int separator = expected.IndexOf(':', StringComparison.Ordinal);
        Assert.Equal([(expected[..separator], expected[(separator + 1)..])], AuthTestAccounts.Fields(error));
        Assert.Empty(await this.ListJobIdsAsync(fixture.Company.Id));
    }

    /// <summary>
    /// O Recrutador sem Empresa aprovada recebe <c>403</c> (ERR-07) com o estado da Empresa; o Candidato e o
    /// Administrador recebem <c>403</c> (ERR-06), como em qualquer endpoint reservado a outro tipo de conta (secção 3.4).
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="companyStatus">Estado da Empresa do Recrutador, ou nulo sem Empresa.</param>
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
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, userType, companyStatus: companyStatus);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, Route, user);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, code);
    }

    /// <summary>
    /// Sem credencial, o pedido recebe <c>401</c> (ERR-01).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AnonymousRequestIsUnauthorized()
    {
        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, Route, user: null);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01");
    }

    /// <summary>
    /// Uma vaga de outra Empresa recebe <c>403</c> (ERR-08) em todas as operações sobre a vaga, sem a alterar (RF059).
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="action">Ação na rota da vaga, ou vazio.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("PUT", "")]
    [InlineData("POST", "/publish")]
    [InlineData("POST", "/suspend")]
    [InlineData("POST", "/republish")]
    [InlineData("POST", "/close")]
    [InlineData("DELETE", "")]
    public async Task JobsOfAnotherCompanyAreForbidden(string method, string action)
    {
        Fixture owner = await this.SeedCompanyAsync();
        Fixture other = await this.SeedCompanyAsync();
        Guid jobId = await this.SeedJobAsync(owner, JobStatus.Published);

        using HttpResponseMessage response = await this.SendAsync(
            new HttpMethod(method),
            $"{Route}/{jobId}{action}",
            other.Recruiter,
            method == "PUT" ? other.ValidBody() : null);

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-08");
        Assert.Equal("Não tem autorização para aceder a este elemento.", error.GetProperty("message").GetString());
        Job job = await this.ReadJobAsync(jobId);
        Assert.Equal(JobStatus.Published, job.Status);
        Assert.Null(job.DeletedAt);
        Assert.Empty(await this.ReadLogAsync(jobId));
    }

    /// <summary>
    /// O ciclo de vida completo: publicar, suspender, voltar a publicar e encerrar, cada transição com o novo estado e o
    /// seu registo; a data da primeira publicação não muda ao voltar a publicar (RF053, RF055, RF056, RF115, RF096).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ValidTransitionsChangeTheStatusAndAreLogged()
    {
        Fixture fixture = await this.SeedCompanyAsync();
        Guid jobId = await this.CreateJobAsync(fixture);

        foreach ((string action, string status) in new[]
        {
            ("publish", "PUBLISHED"),
            ("suspend", "SUSPENDED"),
            ("republish", "PUBLISHED"),
            ("close", "CLOSED"),
        })
        {
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{Route}/{jobId}/{action}", fixture.Recruiter);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement job = await AuthTestAccounts.ReadJsonAsync(response);
            Assert.Equal(status, job.GetProperty("status").GetString());
            Assert.NotEqual(JsonValueKind.Null, job.GetProperty("publishedAt").ValueKind);
        }

        Job stored = await this.ReadJobAsync(jobId);
        Assert.NotNull(stored.FirstPublishedAt);
        Assert.True(stored.PublishedAt > stored.FirstPublishedAt);
        Assert.Equal(
            [
                OperationType.JobCreated,
                OperationType.JobPublished,
                OperationType.JobSuspended,
                OperationType.JobRepublished,
                OperationType.JobClosed,
            ],
            (await this.ReadLogAsync(jobId)).Select(entry => entry.Operation));
    }

    /// <summary>
    /// Qualquer estado diferente de encerrada pode ser encerrado (tabela de 6.5).
    /// </summary>
    /// <param name="status">Estado de origem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(JobStatus.Unpublished)]
    [InlineData(JobStatus.Published)]
    [InlineData(JobStatus.Suspended)]
    public async Task AnyOpenJobCanBeClosed(JobStatus status)
    {
        Fixture fixture = await this.SeedCompanyAsync();
        Guid jobId = await this.SeedJobAsync(fixture, status);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{Route}/{jobId}/close", fixture.Recruiter);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JobStatus.Closed, (await this.ReadJobAsync(jobId)).Status);
        Assert.Equal([(OperationType.JobClosed, fixture.Recruiter.Id)], await this.ReadLogAsync(jobId));
    }

    /// <summary>
    /// As transições fora da tabela de 6.5 e a alteração de uma vaga encerrada recebem <c>409</c> (ERR-30) com o estado
    /// atual, sem mudar a vaga nem registar nada.
    /// </summary>
    /// <param name="status">Estado de origem.</param>
    /// <param name="action">Ação pedida (<c>update</c> é o <c>PUT</c>).</param>
    /// <param name="label">Estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(JobStatus.Unpublished, "suspend", "não publicada")]
    [InlineData(JobStatus.Unpublished, "republish", "não publicada")]
    [InlineData(JobStatus.Published, "publish", "publicada")]
    [InlineData(JobStatus.Published, "republish", "publicada")]
    [InlineData(JobStatus.Suspended, "publish", "suspensa")]
    [InlineData(JobStatus.Suspended, "suspend", "suspensa")]
    [InlineData(JobStatus.Closed, "publish", "encerrada")]
    [InlineData(JobStatus.Closed, "suspend", "encerrada")]
    [InlineData(JobStatus.Closed, "republish", "encerrada")]
    [InlineData(JobStatus.Closed, "close", "encerrada")]
    [InlineData(JobStatus.Closed, "update", "encerrada")]
    public async Task InvalidTransitionsAreRejected(JobStatus status, string action, string label)
    {
        Fixture fixture = await this.SeedCompanyAsync();
        Guid jobId = await this.SeedJobAsync(fixture, status);

        using HttpResponseMessage response = action == "update"
            ? await this.SendAsync(HttpMethod.Put, $"{Route}/{jobId}", fixture.Recruiter, fixture.ValidBody())
            : await this.SendAsync(HttpMethod.Post, $"{Route}/{jobId}/{action}", fixture.Recruiter);

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.Conflict, "ERR-30");
        Assert.Equal($"A operação não é permitida no estado atual: {label}.", error.GetProperty("message").GetString());
        Assert.Equal(status, (await this.ReadJobAsync(jobId)).Status);
        Assert.Empty(await this.ReadLogAsync(jobId));
    }

    /// <summary>
    /// Uma vaga com a data-limite já atingida não é publicada: <c>409</c> (ERR-30) (6.5.4).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ExpiredJobIsNotPublished()
    {
        Fixture fixture = await this.SeedCompanyAsync();
        Guid jobId = await this.SeedJobAsync(fixture, JobStatus.Unpublished, expiresAt: DateTimeOffset.UtcNow.AddHours(-1));

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{Route}/{jobId}/publish", fixture.Recruiter);

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "ERR-30");
        Assert.Equal(JobStatus.Unpublished, (await this.ReadJobAsync(jobId)).Status);
    }

    /// <summary>
    /// A alteração substitui os dados e as listas, não muda o estado e regista <c>JOB_UPDATED</c> (RF054, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateReplacesTheDataKeepsTheStatusAndIsLogged()
    {
        Fixture fixture = await this.SeedCompanyAsync();
        Guid jobId = await this.SeedJobAsync(fixture, JobStatus.Published);
        string body = fixture.ValidJson(
            ("title", "\"Analista de Dados\""),
            ("skillIds", $"[\"{fixture.Sql.Id}\"]"),
            ("benefitIds", "[]"));

        using HttpResponseMessage response = await this.SendRawAsync(HttpMethod.Put, $"{Route}/{jobId}", fixture.Recruiter, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement job = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("Analista de Dados", job.GetProperty("title").GetString());
        Assert.Equal("PUBLISHED", job.GetProperty("status").GetString());
        Assert.Equal([fixture.Sql.Name], job.GetProperty("skills").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.Equal(0, job.GetProperty("benefits").GetArrayLength());
        Job stored = await this.ReadJobAsync(jobId);
        Assert.Equal([fixture.Sql.Id], stored.Skills.Select(skill => skill.Id));
        Assert.Empty(stored.Benefits);
        Assert.Equal([(OperationType.JobUpdated, fixture.Recruiter.Id)], await this.ReadLogAsync(jobId));
    }

    /// <summary>
    /// A alteração usa as validações da criação (RF054, RF051).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateValidatesLikeCreate()
    {
        Fixture fixture = await this.SeedCompanyAsync();
        Guid jobId = await this.SeedJobAsync(fixture, JobStatus.Unpublished);

        using HttpResponseMessage response = await this.SendRawAsync(
            HttpMethod.Put,
            $"{Route}/{jobId}",
            fixture.Recruiter,
            fixture.ValidJson(("minSalary", "5000")));

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10");
        Assert.Equal([("minSalary", "O valor mínimo não pode ser superior ao valor máximo.")], AuthTestAccounts.Fields(error));
        Assert.Empty(await this.ReadLogAsync(jobId));
    }

    /// <summary>
    /// Sem interesses (uma recusa do Candidato não conta), a eliminação devolve <c>204</c>, preenche
    /// <c>deleted_at</c> e regista <c>JOB_DELETED</c>; a vaga sai da lista e passa a dar <c>404</c> (RF058, RF114).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DeletingAJobWithoutInterestsHidesIt()
    {
        Fixture fixture = await this.SeedCompanyAsync();
        Guid jobId = await this.SeedJobAsync(fixture, JobStatus.Published);
        await this.SeedAnswerAsync(fixture, jobId, MatchStatus.DeclinedByCandidate);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Delete, $"{Route}/{jobId}", fixture.Recruiter);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull((await this.ReadJobAsync(jobId)).DeletedAt);
        Assert.Equal([(OperationType.JobDeleted, fixture.Recruiter.Id)], await this.ReadLogAsync(jobId));

        using HttpResponseMessage list = await this.SendAsync(HttpMethod.Get, Route, fixture.Recruiter);
        Assert.Equal(0, (await AuthTestAccounts.ReadJsonAsync(list)).GetArrayLength());
        foreach ((HttpMethod method, string action) in new[]
        {
            (HttpMethod.Put, string.Empty),
            (HttpMethod.Post, "/close"),
            (HttpMethod.Delete, string.Empty),
        })
        {
            using HttpResponseMessage again = await this.SendAsync(
                method,
                $"{Route}/{jobId}{action}",
                fixture.Recruiter,
                method == HttpMethod.Put ? fixture.ValidBody() : null);
            await AssertErrorAsync(again, HttpStatusCode.NotFound, "ERR-20");
        }
    }

    /// <summary>
    /// Com pelo menos um interesse, em qualquer estado da decisão, a eliminação recebe <c>409</c> (ERR-38) e a vaga
    /// fica como estava (RF058).
    /// </summary>
    /// <param name="decision">Estado do interesse.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(MatchStatus.Waiting)]
    [InlineData(MatchStatus.Matched)]
    [InlineData(MatchStatus.RejectedByRecruiter)]
    public async Task DeletingAJobWithInterestsIsRejected(MatchStatus decision)
    {
        Fixture fixture = await this.SeedCompanyAsync();
        Guid jobId = await this.SeedJobAsync(fixture, JobStatus.Published);
        await this.SeedAnswerAsync(fixture, jobId, decision);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Delete, $"{Route}/{jobId}", fixture.Recruiter);

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.Conflict, "ERR-38");
        Assert.Equal("A vaga tem interesses registados e só pode ser encerrada.", error.GetProperty("message").GetString());
        Assert.Null((await this.ReadJobAsync(jobId)).DeletedAt);
        Assert.Empty(await this.ReadLogAsync(jobId));
    }

    /// <summary>
    /// Suspender e encerrar não alteram os interesses em espera, que continuam contados na lista (RF055, RF056, RF113).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SuspendingAndClosingKeepTheWaitingInterests()
    {
        Fixture fixture = await this.SeedCompanyAsync();
        Guid jobId = await this.SeedJobAsync(fixture, JobStatus.Published);
        Guid matchId = await this.SeedAnswerAsync(fixture, jobId, MatchStatus.Waiting);

        foreach (string action in new[] { "suspend", "close" })
        {
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, $"{Route}/{jobId}/{action}", fixture.Recruiter);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, (await AuthTestAccounts.ReadJsonAsync(response)).GetProperty("waitingCandidates").GetInt32());
            Assert.Equal(MatchStatus.Waiting, await this.ReadMatchStatusAsync(matchId));
        }
    }

    /// <summary>
    /// A lista tem só as vagas da própria Empresa, sem as eliminadas, da mais recente para a mais antiga, com os
    /// interesses em espera de cada uma (RF059, RF060).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListShowsOnlyOwnJobsNewestFirst()
    {
        Fixture fixture = await this.SeedCompanyAsync();
        Fixture other = await this.SeedCompanyAsync();
        Guid first = await this.CreateJobAsync(fixture);
        Guid second = await this.CreateJobAsync(fixture);
        Guid deleted = await this.SeedJobAsync(fixture, JobStatus.Unpublished, deleted: true);
        await this.CreateJobAsync(other);
        await this.SeedAnswerAsync(fixture, first, MatchStatus.Waiting);
        await this.SeedAnswerAsync(fixture, first, MatchStatus.Waiting);
        await this.SeedAnswerAsync(fixture, first, MatchStatus.Matched);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, Route, fixture.Recruiter);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<JsonElement> jobs = (await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().ToList();
        Assert.Equal([second, first], jobs.Select(job => job.GetProperty("jobId").GetGuid()));
        Assert.DoesNotContain(deleted, jobs.Select(job => job.GetProperty("jobId").GetGuid()));
        Assert.Equal([0, 2], jobs.Select(job => job.GetProperty("waitingCandidates").GetInt32()));
    }

    private static async Task<JsonElement> AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(code, error.GetProperty("code").GetString());
        return error;
    }

    private static Job NewJob(Fixture fixture, DateTimeOffset? expiresAt = null)
    {
        var job = new Job(
            fixture.Company.Id,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = fixture.Location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
                ExpiresAt = expiresAt,
            });
        job.Skills.Add(fixture.CSharp);
        return job;
    }

    private async Task<Fixture> SeedCompanyAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var account = new AppUser($"recrutador-{Guid.NewGuid():N}@exemplo.pt", "hash", UserType.Recruiter, now, now);
        var recruiter = new Recruiter(account);
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        var company = new Company(
            recruiter.Id,
            new CompanyRegistration
            {
                CompanyName = "Lumen Software, Lda.",
                TaxId = Interlocked.Increment(ref taxIdCounter).ToString(CultureInfo.InvariantCulture),
                Industry = Industry.Technology,
                Address = "Rua de Exemplo, 1, 4700-000 Braga",
                LocationId = location.Id,
                ContactEmail = "geral@lumen-exemplo.pt",
                ContactPhone = "253000000",
                ResponsibleName = "Responsável Exemplo",
            },
            now);
        company.Approve();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var fixture = new Fixture(
            account,
            company,
            location,
            new Skill($"C# {suffix}"),
            new Skill($"SQL {suffix}"),
            new Benefit($"Seguro de saúde {suffix}"));

        await this.SaveAsync(context =>
        {
            context.Locations.Add(location);
            context.Users.Add(account);
            context.Recruiters.Add(recruiter);
            context.Companies.Add(company);
            context.Skills.AddRange(fixture.CSharp, fixture.Sql);
            context.Benefits.Add(fixture.Health);
        });
        return fixture;
    }

    private async Task<Guid> SeedJobAsync(
        Fixture fixture,
        JobStatus status,
        DateTimeOffset? expiresAt = null,
        bool deleted = false)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Job job = NewJob(fixture, expiresAt);
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

        if (deleted)
        {
            job.MarkDeleted(now);
        }

        await this.SaveAsync(context =>
        {
            context.Attach(fixture.CSharp);
            context.Jobs.Add(job);
        });
        return job.Id;
    }

    private async Task<Guid> SeedAnswerAsync(Fixture fixture, Guid jobId, MatchStatus status)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var account = new AppUser($"candidato-{Guid.NewGuid():N}@exemplo.pt", "hash", UserType.Candidate, now, now);
        var candidate = new Candidate(account, "Ana Ferreira", "912345678", fixture.Location.Id);
        Match match = status == MatchStatus.DeclinedByCandidate
            ? Match.Decline(candidate.Id, jobId, now)
            : Match.ExpressInterest(candidate.Id, jobId, now);
        if (status is MatchStatus.Matched or MatchStatus.RejectedByRecruiter)
        {
            match.OpenProfile(now);
            if (status == MatchStatus.Matched)
            {
                match.Accept(fixture.Recruiter.Id, now);
            }
            else
            {
                match.Reject(fixture.Recruiter.Id, now);
            }
        }

        await this.SaveAsync(context =>
        {
            context.Users.Add(account);
            context.Candidates.Add(candidate);
            context.Matches.Add(match);
        });
        return match.Id;
    }

    private async Task<Guid> CreateJobAsync(Fixture fixture)
    {
        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, Route, fixture.Recruiter, fixture.ValidBody());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await AuthTestAccounts.ReadJsonAsync(response)).GetProperty("jobId").GetGuid();
    }

    private async Task SaveAsync(Action<KatchDbContext> add)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        add(context);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Job> ReadJobAsync(Guid jobId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Jobs.IgnoreQueryFilters().AsNoTracking()
            .Include(job => job.Skills)
            .Include(job => job.Benefits)
            .SingleAsync(job => job.Id == jobId, TestContext.Current.CancellationToken);
    }

    private async Task<List<Guid>> ListJobIdsAsync(Guid companyId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Jobs.IgnoreQueryFilters()
            .Where(job => job.CompanyId == companyId)
            .Select(job => job.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<(OperationType Operation, Guid? UserId)>> ReadLogAsync(Guid jobId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return (await context.OperationLogs.AsNoTracking()
                .Where(entry => entry.EntityId == jobId)
                .OrderBy(entry => entry.Id)
                .ToListAsync(TestContext.Current.CancellationToken))
            .Select(entry => (entry.Operation, entry.UserId))
            .ToList();
    }

    private async Task<MatchStatus> ReadMatchStatusAsync(Guid matchId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Matches.AsNoTracking()
            .Where(match => match.Id == matchId)
            .Select(match => match.Status)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, AppUser? user, object? body = null)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (user is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.IssueToken(user));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string route, AppUser user, string json)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.IssueToken(user));
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private string IssueToken(AppUser user)
    {
        return this.factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
    }

    /// <summary>
    /// Recrutador com a Empresa aprovada e as listas de que as vagas precisam, únicas por teste.
    /// </summary>
    private sealed record Fixture(AppUser Recruiter, Company Company, Location Location, Skill CSharp, Skill Sql, Benefit Health)
    {
        public JsonElement ValidBody()
        {
            return JsonSerializer.Deserialize<JsonElement>(this.ValidJson());
        }

        /// <summary>
        /// Pedido válido de 6.5.2 em JSON, com os campos indicados substituídos pelos valores JSON indicados.
        /// </summary>
        public string ValidJson(params (string Field, string Json)[] overrides)
        {
            var fields = new List<(string Name, string Json)>
            {
                ("title", "\"Programador Backend .NET\""),
                ("description", "\"Desenvolvimento e manutenção de APIs REST em ASP.NET Core.\""),
                ("minSalary", "1600.00"),
                ("maxSalary", "2100.00"),
                ("locationId", $"\"{this.Location.Id}\""),
                ("contractType", "\"PERMANENT\""),
                ("workMode", "\"HYBRID\""),
                ("skillIds", $"[\"{this.Sql.Id}\", \"{this.CSharp.Id}\"]"),
                ("benefitIds", $"[\"{this.Health.Id}\"]"),
                ("isUrgent", "true"),
                ("expiresAt", $"\"{DateTimeOffset.UtcNow.AddDays(30):yyyy-MM-ddTHH:mm:ssZ}\""),
            };
            string Resolve(string json) => json
                .Replace("{skill}", this.CSharp.Id.ToString(), StringComparison.Ordinal)
                .Replace("{benefit}", this.Health.Id.ToString(), StringComparison.Ordinal)
                .Replace("{location}", this.Location.Id.ToString(), StringComparison.Ordinal);

            Dictionary<string, string> replaced = overrides.ToDictionary(item => item.Field, item => Resolve(item.Json));
            return "{ " + string.Join(", ", fields.Select(item => $"\"{item.Name}\": {replaced.GetValueOrDefault(item.Name, item.Json)}")) + " }";
        }
    }
}
