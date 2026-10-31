// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

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
/// Testes da consulta dos benefícios e da manutenção das competências e dos benefícios pelo Administrador (documentação
/// da API v02, secções 6.13.3 a 6.13.7; RF099 a RF102, RF104, RF112).
/// </summary>
public sealed class ReferenceListMaintenanceTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string SkillsRoute = "/api/reference-lists/skills";

    private const string BenefitsRoute = "/api/reference-lists/benefits";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceListMaintenanceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação de teste partilhada.</param>
    public ReferenceListMaintenanceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Qualquer tipo de conta consulta os benefícios, por ordem alfabética e só com o identificador e a designação
    /// (6.13.3; RF050).
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Recruiter)]
    [InlineData(UserType.Admin)]
    public async Task BenefitsAreListedInAlphabeticalOrderForAnyAccount(UserType userType)
    {
        // Base de dados própria, para a lista conter só os benefícios deste teste.
        using var isolated = new KatchWebApplicationFactory();
        AppUser user = await AuthTestAccounts.SeedAsync(isolated, userType);
        await SaveAsync(isolated, context => context.Benefits.AddRange(
            new Benefit("Seguro de saúde"),
            new Benefit("Ações de formação"),
            new Benefit("horário flexível")));

        using HttpResponseMessage response = await SendAsync(isolated, HttpMethod.Get, BenefitsRoute, user);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement json = await AuthTestAccounts.ReadJsonAsync(response);
        List<JsonElement> items = json.EnumerateArray().ToList();
        Assert.Equal(
            ["Ações de formação", "horário flexível", "Seguro de saúde"],
            items.Select(item => item.GetProperty("name").GetString()));
        Assert.All(items, item => Assert.Equal(["id", "name"], item.EnumerateObject().Select(property => property.Name)));
    }

    /// <summary>
    /// Sem credencial, a consulta dos benefícios recebe <c>401</c> (ERR-01): só as localidades são anónimas (DAPI-12).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BenefitsRequireASession()
    {
        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Get, BenefitsRoute, user: null);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01");
    }

    /// <summary>
    /// O Administrador acrescenta uma competência: <c>201</c> com o elemento, sem <c>Location</c>, gravada sem os espaços
    /// nas extremidades e com a criação registada com o seu identificador (RF099, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AdminAddsASkill()
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);
        string name = $"Kubernetes {Guid.NewGuid():N}";

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Post, SkillsRoute, admin, new { name = $"  {name}  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        JsonElement json = await AuthTestAccounts.ReadJsonAsync(response);
        Guid id = json.GetProperty("id").GetGuid();
        Assert.Equal(name, json.GetProperty("name").GetString());
        await this.ReadAsync(async context =>
        {
            Assert.Equal(name, (await context.Skills.SingleAsync(skill => skill.Id == id, TestContext.Current.CancellationToken)).Name);
            OperationLog log = await context.OperationLogs.SingleAsync(entry => entry.EntityId == id, TestContext.Current.CancellationToken);
            Assert.Equal(OperationType.SkillCreated, log.Operation);
            Assert.Equal(admin.Id, log.UserId);
        });
    }

    /// <summary>
    /// O Administrador acrescenta um benefício, com a criação registada (RF101, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AdminAddsABenefit()
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);
        string name = $"Horário flexível {Guid.NewGuid():N}";

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Post, BenefitsRoute, admin, new { name });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        JsonElement json = await AuthTestAccounts.ReadJsonAsync(response);
        Guid id = json.GetProperty("id").GetGuid();
        Assert.Equal(name, json.GetProperty("name").GetString());
        await this.ReadAsync(async context =>
        {
            OperationLog log = await context.OperationLogs.SingleAsync(entry => entry.EntityId == id, TestContext.Current.CancellationToken);
            Assert.Equal(OperationType.BenefitCreated, log.Operation);
            Assert.Equal(admin.Id, log.UserId);
        });
    }

    /// <summary>
    /// Designações vazias, só com espaços, em falta ou acima de 100 caracteres são recusadas com <c>400</c> (ERR-10) e o
    /// motivo no campo <c>name</c>, nas duas listas.
    /// </summary>
    /// <param name="route">Lista.</param>
    /// <param name="body">Corpo JSON do pedido.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(SkillsRoute, """{ "name": "" }""", "A designação é obrigatória.")]
    [InlineData(SkillsRoute, """{ "name": "   " }""", "A designação é obrigatória.")]
    [InlineData(SkillsRoute, "{}", "A designação é obrigatória.")]
    [InlineData(SkillsRoute, "", "A designação é obrigatória.")]
    [InlineData(BenefitsRoute, """{ "name": null }""", "A designação é obrigatória.")]
    [InlineData(BenefitsRoute, "LONG", "O texto não pode exceder 100 caracteres.")]
    [InlineData(SkillsRoute, "LONG", "O texto não pode exceder 100 caracteres.")]
    public async Task InvalidNameIsRejected(string route, string body, string reason)
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);
        string json = body == "LONG" ? JsonSerializer.Serialize(new { name = new string('x', 101) }) : body;

        using HttpResponseMessage response = await this.SendRawAsync(HttpMethod.Post, route, admin, json);

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10");
        Assert.Equal([("name", reason)], AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// Uma designação com 100 caracteres é aceite (limite do DM-12).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NameWithMaximumLengthIsAccepted()
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);
        string name = Guid.NewGuid().ToString("N").PadRight(100, 'x');

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Post, BenefitsRoute, admin, new { name });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Uma designação que já existe, com outras maiúsculas, é recusada com <c>400</c> (ERR-10), nas duas listas (RF099,
    /// RF101).
    /// </summary>
    /// <param name="route">Lista.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(SkillsRoute)]
    [InlineData(BenefitsRoute)]
    public async Task DuplicateNameIgnoringCaseIsRejected(string route)
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);
        string name = $"Docker {Guid.NewGuid():N}";
        using HttpResponseMessage created = await SendAsync(this.factory, HttpMethod.Post, route, admin, new { name });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using HttpResponseMessage response = await SendAsync(
            this.factory,
            HttpMethod.Post,
            route,
            admin,
            new { name = name.ToUpperInvariant() });

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "ERR-10");
        Assert.Equal([("name", "A designação já existe na lista.")], AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// A alteração da designação de uma competência mantém as associações aos perfis e às vagas, que passam a apresentar
    /// a nova designação, e é registada com o Administrador (RF100, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenamingASkillKeepsItsProfileAndJobAssociations()
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);
        var skill = new Skill($"Dokcer {Guid.NewGuid():N}");
        (Guid candidateId, Guid jobId) = await this.SeedAssociationsAsync(skill, benefit: null);
        string name = $"Docker {Guid.NewGuid():N}";

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Put, $"{SkillsRoute}/{skill.Id}", admin, new { name });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement json = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(skill.Id, json.GetProperty("id").GetGuid());
        Assert.Equal(name, json.GetProperty("name").GetString());
        await this.ReadAsync(async context =>
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
            Candidate candidate = await context.Candidates.Include(item => item.Skills).SingleAsync(item => item.Id == candidateId, cancellationToken);
            Job job = await context.Jobs.Include(item => item.Skills).SingleAsync(item => item.Id == jobId, cancellationToken);
            Assert.Equal((skill.Id, name), candidate.Skills.Select(item => (item.Id, item.Name)).Single());
            Assert.Equal((skill.Id, name), job.Skills.Select(item => (item.Id, item.Name)).Single());
            OperationLog log = await context.OperationLogs.SingleAsync(entry => entry.EntityId == skill.Id, cancellationToken);
            Assert.Equal(OperationType.SkillRenamed, log.Operation);
            Assert.Equal(admin.Id, log.UserId);
        });
    }

    /// <summary>
    /// A alteração da designação de um benefício mantém as associações às vagas e é registada (RF102, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenamingABenefitKeepsItsJobAssociations()
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);
        var benefit = new Benefit($"Seguro {Guid.NewGuid():N}");
        (_, Guid jobId) = await this.SeedAssociationsAsync(new Skill($"SQL {Guid.NewGuid():N}"), benefit);
        string name = $"Seguro de saúde {Guid.NewGuid():N}";

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Put, $"{BenefitsRoute}/{benefit.Id}", admin, new { name });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await this.ReadAsync(async context =>
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
            Job job = await context.Jobs.Include(item => item.Benefits).SingleAsync(item => item.Id == jobId, cancellationToken);
            Assert.Equal((benefit.Id, name), job.Benefits.Select(item => (item.Id, item.Name)).Single());
            OperationLog log = await context.OperationLogs.SingleAsync(entry => entry.EntityId == benefit.Id, cancellationToken);
            Assert.Equal(OperationType.BenefitRenamed, log.Operation);
        });
    }

    /// <summary>
    /// Mudar só as maiúsculas da própria designação é aceite; a designação de outro elemento da lista é recusada (exemplo
    /// de 6.13.5).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenameChecksUniquenessAgainstTheOtherItemsOnly()
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);
        string suffix = Guid.NewGuid().ToString("N");
        var csharp = new Skill($"c# {suffix}");
        var sql = new Skill($"SQL {suffix}");
        await SaveAsync(this.factory, context => context.Skills.AddRange(csharp, sql));

        using HttpResponseMessage ownCase = await SendAsync(this.factory, HttpMethod.Put, $"{SkillsRoute}/{csharp.Id}", admin, new { name = $"C# {suffix}" });
        using HttpResponseMessage other = await SendAsync(this.factory, HttpMethod.Put, $"{SkillsRoute}/{sql.Id}", admin, new { name = $"c# {suffix}" });

        Assert.Equal(HttpStatusCode.OK, ownCase.StatusCode);
        JsonElement error = await AssertErrorAsync(other, HttpStatusCode.BadRequest, "ERR-10");
        Assert.Equal([("name", "A designação já existe na lista.")], AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// Um identificador inexistente ou mal formado recebe <c>404</c> (ERR-20), nas duas listas.
    /// </summary>
    /// <param name="route">Rota do elemento.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(SkillsRoute + "/2f6c3a3e-0000-4000-8000-000000000001")]
    [InlineData(BenefitsRoute + "/2f6c3a3e-0000-4000-8000-000000000001")]
    public async Task RenamingAnUnknownItemIsNotFound(string route)
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Put, route, admin, new { name = "Ginásio" });

        JsonElement error = await AssertErrorAsync(response, HttpStatusCode.NotFound, "ERR-20");
        Assert.Equal("O elemento pedido não existe ou não está disponível.", error.GetProperty("message").GetString());
    }

    /// <summary>
    /// Um identificador mal formado não corresponde à rota (restrição <c>:guid</c>) e recebe <c>404</c> (documentação da
    /// API v02, secção 2.5). O corpo do <c>404</c> de rota inexistente é do pipeline comum a todos os controllers.
    /// </summary>
    /// <param name="route">Rota com o identificador mal formado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(SkillsRoute + "/nao-e-um-guid")]
    [InlineData(BenefitsRoute + "/123")]
    public async Task MalformedIdentifierIsNotFound(string route)
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(this.factory, UserType.Admin);

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Put, route, admin, new { name = "Ginásio" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// O Candidato e o Recrutador recebem <c>403</c> (ERR-06) nas quatro operações de escrita, sem alterar a lista
    /// (RF104).
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Recruiter)]
    public async Task OnlyTheAdminWritesToTheLists(UserType userType)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, userType);
        var skill = new Skill($"Go {Guid.NewGuid():N}");
        var benefit = new Benefit($"Ginásio {Guid.NewGuid():N}");
        await SaveAsync(this.factory, context =>
        {
            context.Skills.Add(skill);
            context.Benefits.Add(benefit);
        });
        string name = $"Nova {Guid.NewGuid():N}";

        foreach ((HttpMethod method, string route) in new[]
        {
            (HttpMethod.Post, SkillsRoute),
            (HttpMethod.Put, $"{SkillsRoute}/{skill.Id}"),
            (HttpMethod.Post, BenefitsRoute),
            (HttpMethod.Put, $"{BenefitsRoute}/{benefit.Id}"),
        })
        {
            using HttpResponseMessage response = await SendAsync(this.factory, method, route, user, new { name });
            JsonElement error = await AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-06");
            Assert.Equal("A operação não está disponível para este tipo de conta.", error.GetProperty("message").GetString());
        }

        await this.ReadAsync(async context =>
        {
            Assert.False(await context.Skills.AnyAsync(item => item.Name == name, TestContext.Current.CancellationToken));
            Assert.False(await context.Benefits.AnyAsync(item => item.Name == name, TestContext.Current.CancellationToken));
        });
    }

    /// <summary>
    /// Sem credencial, as operações de escrita recebem <c>401</c> (ERR-01).
    /// </summary>
    /// <param name="route">Lista.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(SkillsRoute)]
    [InlineData(BenefitsRoute)]
    public async Task AnonymousWriteIsUnauthorized(string route)
    {
        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Post, route, user: null, new { name = "Rust" });

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01");
    }

    private static async Task<JsonElement> AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(code, error.GetProperty("code").GetString());
        return error;
    }

    private static async Task SaveAsync(KatchWebApplicationFactory target, Action<KatchDbContext> add)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = target.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        add(context);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        KatchWebApplicationFactory target,
        HttpMethod method,
        string route,
        AppUser? user,
        object? body = null)
    {
        using HttpClient client = target.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (user is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(target, user));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string IssueToken(KatchWebApplicationFactory target, AppUser user)
    {
        return target.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string route, AppUser user, string json)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(this.factory, user));
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<(Guid CandidateId, Guid JobId)> SeedAssociationsAsync(Skill skill, Benefit? benefit)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        var candidateAccount = new AppUser($"{Guid.NewGuid():N}@exemplo.pt", "hash", UserType.Candidate, now, now);
        var candidate = new Candidate(candidateAccount, "Ana Ferreira", "912345678", location.Id);
        candidate.AddSkill(skill);
        var recruiterAccount = new AppUser($"{Guid.NewGuid():N}@exemplo.pt", "hash", UserType.Recruiter, now, now);
        var recruiter = new Recruiter(recruiterAccount);
        var company = new Company(
            recruiter.Id,
            new CompanyRegistration
            {
                CompanyName = "Empresa Exemplo, Lda.",
                TaxId = Random.Shared.Next(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Industry = Industry.Technology,
                Address = "Rua de Exemplo, 1, 4700-000 Braga",
                LocationId = location.Id,
                ContactEmail = "geral@empresa-exemplo.pt",
                ContactPhone = "253000000",
                ResponsibleName = "Responsável Exemplo",
            },
            now);
        var job = new Job(
            company.Id,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        job.Skills.Add(skill);
        if (benefit is not null)
        {
            job.Benefits.Add(benefit);
        }

        await SaveAsync(this.factory, context =>
        {
            context.Locations.Add(location);
            context.Skills.Add(skill);
            if (benefit is not null)
            {
                context.Benefits.Add(benefit);
            }

            context.Users.AddRange(candidateAccount, recruiterAccount);
            context.Candidates.Add(candidate);
            context.Recruiters.Add(recruiter);
            context.Companies.Add(company);
            context.Jobs.Add(job);
        });
        return (candidate.Id, job.Id);
    }

    private async Task ReadAsync(Func<KatchDbContext, Task> assert)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        await assert(scope.ServiceProvider.GetRequiredService<KatchDbContext>());
    }
}
