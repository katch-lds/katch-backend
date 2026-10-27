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
/// Testes de <c>POST /api/auth/recruiters</c> sobre a API em memória (documentação da API v02, secção 6.1.2; RF037).
/// </summary>
public sealed class CreateRecruiterAccountTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/auth/recruiters";
    private const string ValidPassword = "Lumen2026rh";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRecruiterAccountTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public CreateRecruiterAccountTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Com dados válidos e sem credencial: 201 sem <c>Location</c>, <c>LoginResponse</c> de Recrutador, conta ativa,
    /// especialização de Recrutador, sem Empresa e palavra-passe só como resumo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ValidRequestReturns201WithSessionAndCreatesAnActiveRecruiterWithoutCompany()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await this.EnsureDatabaseAsync();
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            Route,
            Body("Recrutamento@Lumen-Exemplo.pt"),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement root = json.RootElement;
        Assert.Equal("RECRUITER", root.GetProperty("userType").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("token").GetString()));
        Assert.EndsWith("Z", root.GetProperty("expiresAt").GetString(), StringComparison.Ordinal);
        Guid userId = root.GetProperty("userId").GetGuid();

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        AppUser user = await context.Users.AsNoTracking().SingleAsync(u => u.Id == userId, cancellationToken);
        Assert.Equal(UserType.Recruiter, user.UserType);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.NotNull(user.TermsAcceptedAt);
        Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Verify(user.PasswordHash, ValidPassword));
        Assert.True(await context.Recruiters.AnyAsync(r => r.Id == userId, cancellationToken));
        Assert.False(await context.Candidates.AnyAsync(c => c.Id == userId, cancellationToken));
        Assert.False(await context.Companies.AnyAsync(c => c.RecruiterId == userId, cancellationToken));
    }

    /// <summary>
    /// A credencial devolvida dá acesso às operações do Recrutador, mas as que exigem a Empresa aprovada devolvem o
    /// ERR-07 com a Empresa ainda não registada (RF037, RF039).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NewRecruiterIsAuthenticatedButApprovedCompanyOperationsReturnErr07()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await this.EnsureDatabaseAsync();
        using HttpClient client = this.factory.CreateClient();
        using HttpResponseMessage created = await client.PostAsJsonAsync(
            Route,
            Body($"novo-{Guid.NewGuid():N}@exemplo.pt"),
            cancellationToken);
        using JsonDocument json = JsonDocument.Parse(await created.Content.ReadAsStringAsync(cancellationToken));
        string token = json.RootElement.GetProperty("token").GetString()!;

        using HttpResponseMessage recruiter = await SendAsync(client, "/test-authorization/recruiter", token);
        using HttpResponseMessage approved = await SendAsync(client, "/test-authorization/approved-company", token);

        Assert.Equal(HttpStatusCode.OK, recruiter.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, approved.StatusCode);
        using JsonDocument error = JsonDocument.Parse(await approved.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("ERR-07", error.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "A operação exige a Empresa aprovada. Estado atual da Empresa: não registada.",
            error.RootElement.GetProperty("message").GetString());
    }

    /// <summary>
    /// Correio fora do formato, palavra-passe fora do P05 e condições não aceites: ERR-10 com os três campos.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InvalidFieldsAreReportedInTheSameResponse()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await this.EnsureDatabaseAsync();
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            Route,
            new { email = "recrutamento", password = "curta", acceptTerms = false },
            cancellationToken);

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal(
            [
                ("email", "O endereço de correio eletrónico tem de ter o formato local@domínio."),
                ("password", "A palavra-passe tem de ter pelo menos 8 caracteres, uma letra e um algarismo."),
                ("acceptTerms", "É obrigatório aceitar as condições de utilização."),
            ],
            Fields(error));
    }

    /// <summary>
    /// Sem corpo, os três campos são indicados.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmptyBodyReportsEveryField()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await this.EnsureDatabaseAsync();
        using HttpClient client = this.factory.CreateClient();
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(Route, content, cancellationToken);

        JsonElement error = await AssertErr10Async(response);
        Assert.Equal(["email", "password", "acceptTerms"], Fields(error).Select(field => field.Field));
    }

    /// <summary>
    /// O correio já registado, também com maiúsculas diferentes, é recusado com o ERR-10 e não se cria outra conta.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmailAlreadyRegisteredWithOtherCaseReturnsErr10()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await this.EnsureDatabaseAsync();
        using HttpClient client = this.factory.CreateClient();
        string email = $"duplicado-{Guid.NewGuid():N}@exemplo.pt";
        using HttpResponseMessage first = await client.PostAsJsonAsync(Route, Body(email), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage second = await client.PostAsJsonAsync(
            Route,
            Body(email.ToUpperInvariant()),
            cancellationToken);

        JsonElement error = await AssertErr10Async(second);
        Assert.Equal([("email", "O endereço de correio eletrónico já está registado.")], Fields(error));
    }

    private static object Body(string email)
    {
        return new { email, password = ValidPassword, acceptTerms = true };
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string route, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> AssertErr10Async(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement root = json.RootElement.Clone();
        Assert.Equal("ERR-10", root.GetProperty("code").GetString());
        Assert.Equal("Os dados enviados não cumprem a validação automática.", root.GetProperty("message").GetString());
        return root;
    }

    private static List<(string Field, string Reason)> Fields(JsonElement error)
    {
        return error.GetProperty("fields")
            .EnumerateArray()
            .Select(field => (field.GetProperty("field").GetString()!, field.GetProperty("reason").GetString()!))
            .ToList();
    }

    private async Task EnsureDatabaseAsync()
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }
}
