// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
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
/// Testes de <c>POST /api/auth/candidates</c> sobre a API em memória (documentação da API v02, secção 6.1.1).
/// </summary>
public sealed class RegisterCandidateTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/auth/candidates";
    private const string ValidPassword = "katch2026";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterCandidateTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public RegisterCandidateTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Com dados válidos e sem credencial: 201, <c>Location</c>, <c>LoginResponse</c> com uma credencial aceite pela
    /// API, conta ativa, quota de 10 interesses e palavra-passe só como resumo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ValidRegistrationReturns201WithSessionAndCreatesTheAccount()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            Route,
            Body("Ana.Ferreira@Exemplo.pt", location.Id),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/candidate/profile", response.Headers.Location?.OriginalString);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement root = json.RootElement;
        Assert.Equal("CANDIDATE", root.GetProperty("userType").GetString());
        Guid userId = root.GetProperty("userId").GetGuid();
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("token").GetString()));
        Assert.EndsWith("Z", root.GetProperty("expiresAt").GetString(), StringComparison.Ordinal);
        Assert.False(root.TryGetProperty("password", out _));

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        AppUser user = await context.Users.AsNoTracking().SingleAsync(u => u.Id == userId, cancellationToken);
        Candidate candidate = await context.Candidates.AsNoTracking().SingleAsync(c => c.Id == userId, cancellationToken);
        Assert.Equal(UserType.Candidate, user.UserType);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.NotNull(user.TermsAcceptedAt);
        Assert.NotEqual(ValidPassword, user.PasswordHash);
        Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Verify(user.PasswordHash, ValidPassword));
        Assert.Equal(Candidate.MaxInterests, candidate.SwipeRightsRemaining);
        Assert.Equal(location.Id, candidate.LocationId);
    }

    /// <summary>
    /// A credencial devolvida no registo dá acesso imediato às operações autenticadas (RF005).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TokenReturnedByRegistrationAuthenticatesTheNextRequest()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        using HttpResponseMessage registered = await client.PostAsJsonAsync(
            Route,
            Body($"sessao-{Guid.NewGuid():N}@exemplo.pt", location.Id),
            cancellationToken);
        using JsonDocument json = JsonDocument.Parse(await registered.Content.ReadAsStringAsync(cancellationToken));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/test-authorization/candidate");
        request.Headers.Authorization = new("Bearer", json.RootElement.GetProperty("token").GetString());
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// O correio já registado é recusado com o ERR-10, também com maiúsculas diferentes, e não se cria outra conta.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DuplicateEmailWithOtherCaseReturnsErr10()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Location location = await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        string email = $"duplicado-{Guid.NewGuid():N}@exemplo.pt";
        using HttpResponseMessage first = await client.PostAsJsonAsync(Route, Body(email, location.Id), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage second = await client.PostAsJsonAsync(
            Route,
            Body(email.ToUpperInvariant(), location.Id),
            cancellationToken);

        JsonElement error = await AssertErr10Async(second);
        Assert.Equal(
            [("email", "O endereço de correio eletrónico já está registado.")],
            Fields(error));

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        int accounts = (await context.Users.AsNoTracking().Select(u => u.Email).ToListAsync(cancellationToken))
            .Count(stored => string.Equals(stored, email, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, accounts);
    }

    /// <summary>
    /// Com todos os campos inválidos, o ERR-10 indica cada campo no mesmo pedido, com as mensagens do catálogo (RF004).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AllInvalidFieldsAreReportedInTheSameResponse()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        var body = new
        {
            fullName = " ",
            email = "ana.ferreira",
            phoneNumber = "91234",
            locationId = Guid.NewGuid(),
            password = "curta",
            acceptTerms = false,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(Route, body, cancellationToken);

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal(
            [
                ("fullName", "O campo é obrigatório."),
                ("email", "O endereço de correio eletrónico tem de ter o formato local@domínio."),
                ("phoneNumber", "O contacto telefónico tem de ter 9 algarismos."),
                ("locationId", "O valor não pertence à lista pré-definida."),
                ("password", "A palavra-passe tem de ter pelo menos 8 caracteres, uma letra e um algarismo."),
                ("acceptTerms", "É obrigatório aceitar as condições de utilização."),
            ],
            Fields(error));
    }

    /// <summary>
    /// Sem corpo, todos os campos são indicados como obrigatórios.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmptyBodyReportsEveryFieldAsRequired()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await this.SeedLocationAsync();
        using HttpClient client = this.factory.CreateClient();
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(Route, content, cancellationToken);

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal(
            ["fullName", "email", "phoneNumber", "locationId", "password", "acceptTerms"],
            Fields(error).Select(field => field.Field));
    }

    /// <summary>
    /// Um valor que não pode ser lido no tipo do campo é recusado com o ERR-10 e o campo identificado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnreadableLocationIdReturnsErr10ForThatField()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpClient client = this.factory.CreateClient();
        const string json = """
            {"fullName":"Ana Ferreira","email":"ana@exemplo.pt","phoneNumber":"912345678","locationId":"braga","password":"katch2026","acceptTerms":true}
            """;
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(Route, content, cancellationToken);

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal([("locationId", "O valor não pertence à lista pré-definida.")], Fields(error));
    }

    private static object Body(string email, Guid locationId)
    {
        return new
        {
            fullName = "Ana Ferreira",
            email,
            phoneNumber = "912345678",
            locationId,
            password = ValidPassword,
            acceptTerms = true,
        };
    }

    private static async Task<JsonElement> AssertErr10Async(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement root = json.RootElement.Clone();
        Assert.Equal("ERR-10", root.GetProperty("code").GetString());
        Assert.Equal("Os dados enviados não cumprem a validação automática.", root.GetProperty("message").GetString());
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

    private async Task<Location> SeedLocationAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var location = new Location($"Guimarães {Guid.NewGuid():N}", "Braga", 41.444m, -8.296m);

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        context.Locations.Add(location);
        await context.SaveChangesAsync(cancellationToken);
        return location;
    }
}
