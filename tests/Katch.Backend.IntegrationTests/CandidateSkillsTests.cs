// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Headers;
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
/// Testes da consulta das competências (documentação da API v02, secção 6.13.2) e das competências do perfil do
/// Candidato (secções 6.2.4, 6.2.5, 6.2.10 e 6.2.11) sobre a API em memória. Os pedidos concorrentes sobre a mesma
/// competência são exercitados nos testes unitários, porque o índice <c>uq_candidate_custom_skill</c> sobre
/// <c>lower(label)</c> só existe no PostgreSQL.
/// </summary>
public sealed class CandidateSkillsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string SkillsRoute = "/api/reference-lists/skills";
    private const string ProfileSkillsRoute = "/api/candidate/profile/skills";
    private const string CustomSkillsRoute = "/api/candidate/profile/custom-skills";
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";
    private const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";
    private const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";
    private const string Required = "O campo é obrigatório.";
    private const string NotInList = "O valor não pertence à lista pré-definida.";
    private const string AlreadyAdded = "A competência já está associada ao perfil.";
    private const string LabelFormat =
        "A competência tem de ter entre 2 e 40 caracteres e conter apenas letras, algarismos, espaços e os símbolos + # . -";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateSkillsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public CandidateSkillsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Gets the profile endpoints, for the access tests: method, route suffix and JSON body.
    /// </summary>
    public static TheoryData<string, string, string?> ProfileEndpoints => new()
    {
        { "POST", "skills", "{\"skillId\":\"7c6b5a49-3827-4615-a0b9-c8d7e6f5a410\"}" },
        { "DELETE", "skills/7c6b5a49-3827-4615-a0b9-c8d7e6f5a410", null },
        { "POST", "custom-skills", "{\"label\":\"Blazor\"}" },
        { "DELETE", "custom-skills/b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d15", null },
    };

    /// <summary>
    /// Gets designações «Outro» fora do P07: 1 e 41 caracteres, «@» e só espaços.
    /// </summary>
    public static TheoryData<string> InvalidLabels => new()
    {
        "C",
        new string('a', 41),
        "ana@exemplo",
        "   ",
    };

    /// <summary>
    /// A lista de competências é consultada por qualquer tipo de conta, por ordem alfabética.
    /// </summary>
    /// <param name="userType">Tipo de conta da sessão.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Recruiter)]
    [InlineData(UserType.Admin)]
    public async Task SkillsAreListedForAnyAccountInAlphabeticalOrder(UserType userType)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Skill sql = await this.SeedSkillAsync($"SQL {suffix}");
        Skill csharp = await this.SeedSkillAsync($"C# {suffix}");
        Skill docker = await this.SeedSkillAsync($"Docker {suffix}");
        string token = await this.SeedTokenAsync(userType);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, SkillsRoute, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement items = await AuthTestAccounts.ReadJsonAsync(response);
        List<string> names = items.EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToList();
        Assert.Equal(names.Order(StringComparer.Create(new System.Globalization.CultureInfo("pt-PT"), true)), names);
        List<Guid> ours = items.EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Where(id => id == sql.Id || id == csharp.Id || id == docker.Id)
            .ToList();
        Assert.Equal([csharp.Id, docker.Id, sql.Id], ours);
        Assert.All(items.EnumerateArray(), item => Assert.Equal(2, item.EnumerateObject().Count()));
    }

    /// <summary>
    /// Sem credencial, a lista de competências não é devolvida (ERR-01).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SkillsRequireASession()
    {
        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, SkillsRoute, token: null);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSessionMessage);
    }

    /// <summary>
    /// Uma competência da lista é associada ao perfil: 201 com o <c>CandidateProfileDto</c> e a linha em
    /// <c>candidate_skill</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddSkillReturns201WithTheProfileAndStoresTheAssociation()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        Skill skill = await this.SeedSkillAsync($"C# {Guid.NewGuid():N}");

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, ProfileSkillsRoute, token, new { skillId = skill.Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement profile = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(candidate.Id, profile.GetProperty("candidateId").GetGuid());
        Assert.Equal(candidate.User.Email, profile.GetProperty("email").GetString());
        Assert.StartsWith("Guimarães ", profile.GetProperty("locationName").GetString(), StringComparison.Ordinal);
        Assert.EndsWith(" (Braga)", profile.GetProperty("locationName").GetString(), StringComparison.Ordinal);
        JsonElement added = Assert.Single(profile.GetProperty("skills").EnumerateArray());
        Assert.Equal(skill.Id, added.GetProperty("id").GetGuid());
        Assert.Equal(skill.Name, added.GetProperty("name").GetString());
        Assert.Empty(profile.GetProperty("customSkills").EnumerateArray());
        Assert.False(profile.GetProperty("hasCv").GetBoolean());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("photoUrl").ValueKind);
        Assert.Equal([skill.Id], await this.ReadSkillIdsAsync(candidate.Id));
    }

    /// <summary>
    /// Uma competência fora da lista, já associada, em falta ou ilegível é recusada com ERR-10 e o motivo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddSkillRejectsUnknownRepeatedMissingAndUnreadableSkills()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        Skill skill = await this.SeedSkillAsync($"SQL {Guid.NewGuid():N}");
        using (HttpResponseMessage first = await this.SendAsync(HttpMethod.Post, ProfileSkillsRoute, token, new { skillId = skill.Id }))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        await AssertErr10Async(await this.SendAsync(HttpMethod.Post, ProfileSkillsRoute, token, new { skillId = Guid.NewGuid() }), "skillId", NotInList);
        await AssertErr10Async(await this.SendAsync(HttpMethod.Post, ProfileSkillsRoute, token, new { skillId = skill.Id }), "skillId", AlreadyAdded);
        await AssertErr10Async(await this.SendAsync(HttpMethod.Post, ProfileSkillsRoute, token, new { }), "skillId", Required);
        await AssertErr10Async(await this.SendRawAsync(HttpMethod.Post, ProfileSkillsRoute, token, string.Empty), "skillId", Required);
        await AssertErr10Async(await this.SendRawAsync(HttpMethod.Post, ProfileSkillsRoute, token, "{\"skillId\":\"csharp\"}"), "skillId", NotInList);
        Assert.Equal([skill.Id], await this.ReadSkillIdsAsync(candidate.Id));
    }

    /// <summary>
    /// A competência associada é retirada com 204; a que não está no perfil dá 404 (ERR-20).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RemoveSkillReturns204AndThen404()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        Skill skill = await this.SeedSkillAsync($"Docker {Guid.NewGuid():N}");
        using (HttpResponseMessage added = await this.SendAsync(HttpMethod.Post, ProfileSkillsRoute, token, new { skillId = skill.Id }))
        {
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }

        using HttpResponseMessage removed = await this.SendAsync(HttpMethod.Delete, $"{ProfileSkillsRoute}/{skill.Id}", token);
        using HttpResponseMessage again = await this.SendAsync(HttpMethod.Delete, $"{ProfileSkillsRoute}/{skill.Id}", token);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await AuthenticationTests.AssertErrorAsync(again, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
        Assert.Empty(await this.ReadSkillIdsAsync(candidate.Id));
        Assert.True(await this.SkillExistsAsync(skill.Id));
    }

    /// <summary>
    /// A opção «Outro» aceita «C#» e os limites de 2 e 40 caracteres, guarda em <c>candidate_custom_skill</c> e devolve o
    /// perfil com as competências por ordem alfabética.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddCustomSkillAcceptsCSharpAndTheLengthLimits()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        string forty = new('x', 40);

        foreach (string label in new[] { "C#", "Go", forty })
        {
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, CustomSkillsRoute, token, new { label });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        using HttpResponseMessage last = await this.SendAsync(HttpMethod.Post, CustomSkillsRoute, token, new { label = "Blazor" });
        Assert.Equal(HttpStatusCode.Created, last.StatusCode);
        JsonElement profile = await AuthTestAccounts.ReadJsonAsync(last);
        Assert.Equal(
            ["Blazor", "C#", "Go", forty],
            profile.GetProperty("customSkills").EnumerateArray().Select(item => item.GetProperty("label").GetString()));
        Assert.Equal(
            ["Blazor", "C#", "Go", forty],
            (await this.ReadCustomSkillsAsync(candidate.Id)).Select(item => item.Label).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// As designações fora do P07 são recusadas com ERR-10 e o motivo do catálogo, sem gravar (RF009).
    /// </summary>
    /// <param name="label">Designação.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(InvalidLabels))]
    public async Task AddCustomSkillRejectsLabelsOutsideP07(string label)
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();

        await AssertErr10Async(await this.SendAsync(HttpMethod.Post, CustomSkillsRoute, token, new { label }), "label", LabelFormat);
        Assert.Empty(await this.ReadCustomSkillsAsync(candidate.Id));
    }

    /// <summary>
    /// A mesma designação com outras maiúsculas e o pedido sem <c>label</c> são recusados com ERR-10.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddCustomSkillRejectsRepeatedAndMissingLabels()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        using (HttpResponseMessage first = await this.SendAsync(HttpMethod.Post, CustomSkillsRoute, token, new { label = "Blazor" }))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        await AssertErr10Async(await this.SendAsync(HttpMethod.Post, CustomSkillsRoute, token, new { label = "BLAZOR" }), "label", AlreadyAdded);
        await AssertErr10Async(await this.SendAsync(HttpMethod.Post, CustomSkillsRoute, token, new { }), "label", Required);
        Assert.Single(await this.ReadCustomSkillsAsync(candidate.Id));
    }

    /// <summary>
    /// A competência «Outro» é retirada com 204; a inexistente, ou a de outro Candidato, dá 404 (ERR-20).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RemoveCustomSkillReturns204OnlyForOwnSkills()
    {
        (Candidate candidate, string token) = await this.SeedCandidateAsync();
        (Candidate other, string otherToken) = await this.SeedCandidateAsync();
        using (HttpResponseMessage added = await this.SendAsync(HttpMethod.Post, CustomSkillsRoute, otherToken, new { label = "Kotlin" }))
        {
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }

        using (HttpResponseMessage added = await this.SendAsync(HttpMethod.Post, CustomSkillsRoute, token, new { label = "Kotlin" }))
        {
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }

        Guid own = Assert.Single(await this.ReadCustomSkillsAsync(candidate.Id)).Id;
        Guid others = Assert.Single(await this.ReadCustomSkillsAsync(other.Id)).Id;

        using HttpResponseMessage foreign = await this.SendAsync(HttpMethod.Delete, $"{CustomSkillsRoute}/{others}", token);
        using HttpResponseMessage removed = await this.SendAsync(HttpMethod.Delete, $"{CustomSkillsRoute}/{own}", token);

        await AuthenticationTests.AssertErrorAsync(foreign, HttpStatusCode.NotFound, "ERR-20", NotFoundMessage);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await this.ReadCustomSkillsAsync(candidate.Id));
        Assert.Single(await this.ReadCustomSkillsAsync(other.Id));
    }

    /// <summary>
    /// As contas de Recrutador e de Administrador recebem o ERR-06 nos quatro endpoints do perfil.
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="relativePath">Rota a seguir a <c>/api/candidate/profile/</c>.</param>
    /// <param name="body">Corpo JSON, quando existe.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(ProfileEndpoints))]
    public async Task OtherAccountTypesReturnErr06(string method, string relativePath, string? body)
    {
        foreach (UserType userType in new[] { UserType.Recruiter, UserType.Admin })
        {
            string token = await this.SeedTokenAsync(userType);

            using HttpResponseMessage response = await this.SendRawAsync(new HttpMethod(method), $"/api/candidate/profile/{relativePath}", token, body);

            await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-06", Err06Message);
        }
    }

    /// <summary>
    /// Sem credencial, os quatro endpoints do perfil devolvem o ERR-01.
    /// </summary>
    /// <param name="method">Método HTTP.</param>
    /// <param name="relativePath">Rota a seguir a <c>/api/candidate/profile/</c>.</param>
    /// <param name="body">Corpo JSON, quando existe.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(ProfileEndpoints))]
    public async Task ProfileEndpointsRequireASession(string method, string relativePath, string? body)
    {
        using HttpResponseMessage response = await this.SendRawAsync(new HttpMethod(method), $"/api/candidate/profile/{relativePath}", token: null, body);

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "ERR-01", InvalidSessionMessage);
    }

    private static async Task AssertErr10Async(HttpResponseMessage response, string field, string reason)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
            Assert.Equal("ERR-10", error.GetProperty("code").GetString());
            Assert.Equal([(field, reason)], AuthTestAccounts.Fields(error));
        }
    }

    private async Task<(Candidate Candidate, string Token)> SeedCandidateAsync()
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@exemplo.pt", "hash", UserType.Candidate, created, created);
        var location = new Location($"Guimarães {Guid.NewGuid():N}", "Braga", 41.444m, -8.296m);
        var candidate = new Candidate(user, "Ana Ferreira", "912345678", location.Id);
        await this.SaveAsync(context =>
        {
            context.Locations.Add(location);
            context.Candidates.Add(candidate);
        });
        return (candidate, this.IssueToken(user));
    }

    private async Task<string> SeedTokenAsync(UserType userType)
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@exemplo.pt", "hash", userType, created, created);
        await this.SaveAsync(context =>
        {
            switch (userType)
            {
                case UserType.Candidate:
                    var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545m, -8.426m);
                    context.Locations.Add(location);
                    context.Candidates.Add(new Candidate(user, "Ana Ferreira", "912345678", location.Id));
                    break;
                case UserType.Recruiter:
                    context.Recruiters.Add(new Recruiter(user));
                    break;
                default:
                    context.Admins.Add(new Admin(user));
                    break;
            }
        });
        return this.IssueToken(user);
    }

    private async Task<Skill> SeedSkillAsync(string name)
    {
        var skill = new Skill(name);
        await this.SaveAsync(context => context.Skills.Add(skill));
        return skill;
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

    private async Task<List<Guid>> ReadSkillIdsAsync(Guid candidateId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        Candidate candidate = await context.Candidates
            .Include(item => item.Skills)
            .AsNoTracking()
            .SingleAsync(item => item.Id == candidateId, TestContext.Current.CancellationToken);
        return candidate.Skills.Select(skill => skill.Id).ToList();
    }

    private async Task<List<CandidateCustomSkill>> ReadCustomSkillsAsync(Guid candidateId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.CandidateCustomSkills
            .AsNoTracking()
            .Where(item => item.CandidateId == candidateId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<bool> SkillExistsAsync(Guid skillId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Skills.AnyAsync(skill => skill.Id == skillId, TestContext.Current.CancellationToken);
    }

    private string IssueToken(AppUser user)
    {
        return this.factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, string? token, object? body = null)
    {
        return this.SendRawAsync(method, route, token, body is null ? null : JsonSerializer.Serialize(body));
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string route, string? token, string? body)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
