// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Text;
using System.Text.Json;
using Katch.Backend.Infrastructure.Hosting;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes da infraestrutura de alojamento: verificações de saúde (ROB-005), limite global do corpo (ROB-004),
/// filtragem do cabeçalho <c>Host</c> (SEG-015) e cabeçalhos do proxy (ROB-003).
/// </summary>
public sealed class HostingTests : IClassFixture<KatchWebApplicationFactory>
{
    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostingTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public HostingTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// As verificações de vida e de prontidão respondem 200 <c>Healthy</c> sem credencial.
    /// </summary>
    /// <param name="route">Rota da verificação.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(HostingServiceCollectionExtensions.LivenessRoute)]
    [InlineData(HostingServiceCollectionExtensions.ReadinessRoute)]
    public async Task HealthChecksAreAnonymousAndHealthy(string route)
    {
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri(route, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Um pedido JSON acima de 64 KB é recusado com 413 ERR-12, antes da leitura do corpo e da autenticação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task JsonBodyAboveTheLimitIs413Err12()
    {
        using HttpClient client = this.factory.CreateClient();
        string body = "{\"email\":\"" + new string('a', 65_536) + "\",\"password\":\"x\"}";
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("ERR-12", json.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Os carregamentos têm limite próprio: um envio acima de 64 KB não é recusado pelo limite global (sem credencial,
    /// a resposta é a da autenticação).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UploadsKeepTheirOwnLimit()
    {
        using HttpClient client = this.factory.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(new byte[100_000]), "file", "foto.png" },
        };

        using HttpResponseMessage response = await client.PutAsync(
            new Uri("/api/candidate/profile/photo", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Um cabeçalho <c>Host</c> fora de <c>AllowedHosts</c> é recusado com 400.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownHostIsRejected()
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Host = "katch.exemplo.invalid";

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Com <c>ForwardedHeaders:Enabled</c>, o IP do cliente e o esquema vêm do último valor dos cabeçalhos do proxy.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ForwardedHeadersAreReadWhenEnabled()
    {
        using var enabled = this.factory.WithWebHostBuilder(builder =>
            builder.UseSetting(HostingServiceCollectionExtensions.ForwardedHeadersEnabledKey, "true"));

        JsonElement info = await GetRequestInfoAsync(enabled.CreateClient());

        Assert.Equal("203.0.113.7", info.GetProperty("remoteIp").GetString());
        Assert.Equal("https", info.GetProperty("scheme").GetString());
    }

    /// <summary>
    /// Sem a opção, os cabeçalhos do proxy são ignorados: um cliente não consegue escolher o IP com que é visto.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ForwardedHeadersAreIgnoredByDefault()
    {
        JsonElement info = await GetRequestInfoAsync(this.factory.CreateClient());

        Assert.NotEqual("203.0.113.7", info.GetProperty("remoteIp").GetString());
        Assert.Equal("http", info.GetProperty("scheme").GetString());
    }

    private static async Task<JsonElement> GetRequestInfoAsync(HttpClient client)
    {
        using (client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/test-request-info", UriKind.Relative));
            request.Headers.Add("X-Forwarded-For", "198.51.100.1, 203.0.113.7");
            request.Headers.Add("X-Forwarded-Proto", "https");

            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            return json.RootElement.Clone();
        }
    }
}
