// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de <c>GET /api/reference-lists/locations</c> sobre a API em memória (documentação da API v02, secção 6.13.1).
/// </summary>
public sealed class ReferenceListsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/reference-lists/locations";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceListsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public ReferenceListsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Sem credencial, a lista responde 200 por ordem alfabética, só com o identificador e a designação (RNF006).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LocationsAreListedWithoutCredentials()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await this.SeedLocationsAsync();
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Route, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        List<JsonElement> items = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(
            ["Braga (Braga)", "Évora (Évora)", "Guimarães (Braga)"],
            items.Select(item => item.GetProperty("name").GetString()));
        Assert.All(
            items,
            item => Assert.Equal(["id", "name"], item.EnumerateObject().Select(property => property.Name)));
    }

    /// <summary>
    /// Uma credencial inválida enviada no pedido não é verificada: a lista continua a responder 200.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InvalidCredentialIsIgnored()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await this.SeedLocationsAsync();
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "nao.e.uma.credencial");

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task SeedLocationsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        if (!context.Locations.Any())
        {
            context.Locations.AddRange(
                new Location("Guimarães", "Braga", 41.444m, -8.296m),
                new Location("Évora", "Évora", 38.571m, -7.909m),
                new Location("Braga", "Braga", 41.545m, -8.426m));
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
