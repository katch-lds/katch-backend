// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Katch.Backend.Infrastructure.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes da exposição da API: erro inesperado sem pormenores (SEG-004), Swagger só com a opção (SEG-004), cabeçalhos
/// de segurança e HSTS (SEG-005), texto com caracteres de controlo (SEG-006) e tipo de conteúdo errado em endpoints
/// anónimos (FUN-001).
/// </summary>
public sealed class ExposureTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string ProductionHost = "api.katch.test";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExposureTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public ExposureTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Uma exceção não tratada dá 500 ERR-50 com a mensagem do catálogo, sem o texto nem o tipo da exceção, e com os
    /// cabeçalhos de segurança.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnhandledExceptionIsErr50WithoutDetails()
    {
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/test-request-info/failure", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(body);
        Assert.Equal("ERR-50", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Ocorreu um erro inesperado. Tente novamente.", json.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("segredo", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    /// <summary>
    /// Todas as respostas trazem <c>nosniff</c>, <c>X-Frame-Options</c>, <c>Referrer-Policy</c> e a CSP restritiva.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ResponsesCarryTheSecurityHeaders()
    {
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/auth/password", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal(
            "default-src 'none'; frame-ancestors 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    /// <summary>
    /// O Swagger UI não recebe a CSP restritiva (precisa dos seus scripts), mas mantém os restantes cabeçalhos.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SwaggerUiHasNoRestrictiveCsp()
    {
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/swagger/index.html", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    /// <summary>
    /// Sem <c>Swagger:Enabled</c>, o documento OpenAPI não é publicado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SwaggerIsNotPublishedWithoutTheOption()
    {
        using var isolated = new KatchWebApplicationFactory();
        using WebApplicationFactory<Program> disabled = isolated.WithWebHostBuilder(builder =>
            builder.UseSetting(HostingServiceCollectionExtensions.SwaggerEnabledKey, "false"));
        using HttpClient client = disabled.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/swagger/v1/swagger.json", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(
            "openapi",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Em produção, um pedido HTTPS (visto através do proxy) recebe o HSTS.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ProductionHttpsResponsesCarryHsts()
    {
        using var isolated = new KatchWebApplicationFactory();
        using WebApplicationFactory<Program> production = isolated.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("AllowedHosts", ProductionHost)
            .UseSetting(HostingServiceCollectionExtensions.ForwardedHeadersEnabledKey, "true"));
        using HttpClient client = production.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Host = ProductionHost;
        request.Headers.Add("X-Forwarded-Proto", "https");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("max-age=", response.Headers.GetValues("Strict-Transport-Security").Single(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Um endpoint anónimo com o <c>Content-Type</c> errado responde 415 ERR-13, e não 401 (FUN-001); um endpoint
    /// autenticado também, mesmo sem credencial.
    /// </summary>
    /// <param name="route">Rota do endpoint JSON.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/candidates")]
    [InlineData("/api/auth/recruiters")]
    [InlineData("/api/conversations/9d8c7b6a-5f4e-4d3c-b2a1-0f9e8d7c6b06/messages")]
    public async Task WrongContentTypeIs415Err13(string route)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(route, UriKind.Relative))
        {
            Content = new StringContent("email=a@b.pt", Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("X-Client-App", "web");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("ERR-13", json.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Um texto com caracteres de controlo é recusado com ERR-10 no campo, com a razão própria (SEG-006).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ControlCharactersAreRejectedInTheField()
    {
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/auth/recruiters", UriKind.Relative),
            new { email = "ana\u0000@exemplo.pt", password = "Mar3-Azul-Faro", acceptTerms = true },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-10", error.GetProperty("code").GetString());
        Assert.Equal(
            [("email", "O texto contém caracteres não permitidos.")],
            AuthTestAccounts.Fields(error));
    }

    /// <summary>
    /// A tabulação e as mudanças de linha continuam aceites no texto livre (uma mensagem com parágrafos, por exemplo).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TabsAndLineBreaksAreAccepted()
    {
        await AuthTestAccounts.SeedAsync(this.factory, Domain.Enums.UserType.Admin);
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/auth/recruiters", UriKind.Relative),
            new { email = $"ana-{Guid.NewGuid():N}@exemplo.pt", password = "Linha\r\n\tdois2", acceptTerms = true },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
