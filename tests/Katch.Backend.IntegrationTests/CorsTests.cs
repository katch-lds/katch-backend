// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes da política CORS do frontend web (I130): preflight, pedidos de uma origem autorizada e não autorizada e
/// cabeçalhos CORS nas respostas de erro. A fábrica autoriza <see cref="KatchWebApplicationFactory.AllowedOrigin"/> e
/// <c>http://localhost:5173</c>, configurada com a barra final.
/// </summary>
public sealed class CorsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string OtherAllowedOrigin = "http://localhost:5173";
    private const string ForeignOrigin = "https://exemplo.pt";
    private const string AllowOrigin = "Access-Control-Allow-Origin";
    private const string AllowMethods = "Access-Control-Allow-Methods";
    private const string AllowHeaders = "Access-Control-Allow-Headers";
    private const string AllowCredentials = "Access-Control-Allow-Credentials";
    private const string ExposeHeaders = "Access-Control-Expose-Headers";
    private const string LocationsRoute = "/api/reference-lists/locations";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public CorsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// O preflight de uma origem autorizada, para cada método da API, responde 204 sem credencial, com
    /// <c>Access-Control-Allow-Origin</c> igual à origem, os métodos GET, POST, PUT e DELETE e os cabeçalhos
    /// Authorization, Content-Type e X-Client-App, e sem autorizar credenciais por cookie. Inclui um endpoint protegido,
    /// porque o browser nunca envia o token no preflight.
    /// </summary>
    /// <param name="origin">Origem do pedido.</param>
    /// <param name="method">Método pedido no preflight.</param>
    /// <param name="route">Rota do pedido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(KatchWebApplicationFactory.AllowedOrigin, "POST", AuthTestAccounts.LoginRoute)]
    [InlineData(KatchWebApplicationFactory.AllowedOrigin, "GET", "/api/candidate/profile")]
    [InlineData(KatchWebApplicationFactory.AllowedOrigin, "PUT", "/api/company")]
    [InlineData(KatchWebApplicationFactory.AllowedOrigin, "DELETE", "/api/jobs/00000000-0000-0000-0000-000000000001")]
    [InlineData(OtherAllowedOrigin, "POST", AuthTestAccounts.LoginRoute)]
    public async Task PreflightFromAllowedOriginSucceeds(string origin, string method, string route)
    {
        using HttpResponseMessage response = await this.PreflightAsync(origin, method, route, "authorization, content-type, x-client-app");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, Header(response, AllowOrigin));
        Assert.Equal(["GET", "POST", "PUT", "DELETE"], HeaderList(response, AllowMethods));
        Assert.Equal(["authorization", "content-type", "x-client-app"], HeaderList(response, AllowHeaders).Select(h => h.ToLowerInvariant()));
        Assert.Null(Header(response, AllowCredentials));
    }

    /// <summary>
    /// O preflight de uma origem não autorizada (outro anfitrião, porto ou esquema, ou a origem opaca <c>null</c>) não
    /// recebe nenhum cabeçalho CORS, pelo que o browser bloqueia o pedido.
    /// </summary>
    /// <param name="origin">Origem do pedido.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(ForeignOrigin)]
    [InlineData("http://localhost:4174")]
    [InlineData("https://localhost:4173")]
    [InlineData("null")]
    public async Task PreflightFromForeignOriginGetsNoCorsHeaders(string origin)
    {
        using HttpResponseMessage response = await this.PreflightAsync(origin, "POST", AuthTestAccounts.LoginRoute, "content-type, x-client-app");

        Assert.Null(Header(response, AllowOrigin));
        Assert.Null(Header(response, AllowMethods));
        Assert.Null(Header(response, AllowHeaders));
    }

    /// <summary>
    /// Num preflight de uma origem autorizada que pede um método ou um cabeçalho que a API não usa, a resposta só lista
    /// os métodos e os cabeçalhos da política; o pedido não consta, pelo que o browser o bloqueia.
    /// </summary>
    /// <param name="method">Método pedido no preflight.</param>
    /// <param name="headers">Cabeçalhos pedidos no preflight.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("PATCH", "content-type")]
    [InlineData("POST", "content-type, x-outro")]
    public async Task PreflightForOtherMethodOrHeaderDoesNotAllowIt(string method, string headers)
    {
        using HttpResponseMessage response = await this.PreflightAsync(
            KatchWebApplicationFactory.AllowedOrigin,
            method,
            AuthTestAccounts.LoginRoute,
            headers);

        Assert.DoesNotContain("PATCH", HeaderList(response, AllowMethods));
        Assert.DoesNotContain("x-outro", HeaderList(response, AllowHeaders).Select(h => h.ToLowerInvariant()));
    }

    /// <summary>
    /// Um pedido de uma origem autorizada recebe <c>Access-Control-Allow-Origin</c> igual à origem, expõe o cabeçalho
    /// <c>Location</c> (lido pelo frontend web nos carregamentos) e indica que a resposta varia com a origem.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RequestFromAllowedOriginGetsCorsHeaders()
    {
        using HttpResponseMessage response = await this.GetLocationsAsync(KatchWebApplicationFactory.AllowedOrigin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(KatchWebApplicationFactory.AllowedOrigin, Header(response, AllowOrigin));
        Assert.Equal(["Location"], HeaderList(response, ExposeHeaders));
        Assert.Contains("Origin", response.Headers.Vary);
        Assert.Null(Header(response, AllowCredentials));
    }

    /// <summary>
    /// Um pedido de uma origem não autorizada não recebe <c>Access-Control-Allow-Origin</c>: o browser não entrega a
    /// resposta ao script.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RequestFromForeignOriginGetsNoCorsHeaders()
    {
        using HttpResponseMessage response = await this.GetLocationsAsync(ForeignOrigin);

        Assert.Null(Header(response, AllowOrigin));
        Assert.Null(Header(response, ExposeHeaders));
    }

    /// <summary>
    /// As respostas de erro no formato ApiError (400 do início de sessão sem X-Client-App, 401 sem credencial, 403 da
    /// política de autorização e 403 da conta bloqueada) também trazem os cabeçalhos CORS para a origem autorizada, para
    /// que o frontend web consiga ler a mensagem.
    /// </summary>
    /// <param name="scenario">Caso de erro.</param>
    /// <param name="status">Estado HTTP esperado.</param>
    /// <param name="code">Código de erro esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("sem-x-client-app", HttpStatusCode.BadRequest, "ERR-10")]
    [InlineData("sem-credencial", HttpStatusCode.Unauthorized, "ERR-01")]
    [InlineData("tipo-de-conta", HttpStatusCode.Forbidden, "ERR-06")]
    [InlineData("conta-bloqueada", HttpStatusCode.Forbidden, "ERR-03")]
    public async Task ErrorResponsesCarryCorsHeaders(string scenario, HttpStatusCode status, string code)
    {
        using HttpRequestMessage request = await this.ErrorRequestAsync(scenario);
        request.Headers.Add("Origin", KatchWebApplicationFactory.AllowedOrigin);
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(KatchWebApplicationFactory.AllowedOrigin, Header(response, AllowOrigin));
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(code, error.GetProperty("code").GetString());
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? string.Join(",", values) : null;

    private static List<string> HeaderList(HttpResponseMessage response, string name) =>
        [.. (Header(response, name) ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private async Task<HttpResponseMessage> PreflightAsync(string origin, string method, string route, string headers)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri(route, UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> GetLocationsAsync(string origin)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(LocationsRoute, UriKind.Relative));
        request.Headers.Add("Origin", origin);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpRequestMessage> ErrorRequestAsync(string scenario)
    {
        switch (scenario)
        {
            case "sem-x-client-app":
                return new HttpRequestMessage(HttpMethod.Post, new Uri(AuthTestAccounts.LoginRoute, UriKind.Relative))
                {
                    Content = JsonContent.Create(AuthTestAccounts.Credentials("candidato@exemplo.pt", AuthTestAccounts.Password)),
                };
            case "sem-credencial":
                return new HttpRequestMessage(HttpMethod.Get, new Uri("/test-authorization/any", UriKind.Relative));
            case "tipo-de-conta":
                return this.Authorized("/test-authorization/admin", await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate));
            default:
                AppUser blocked = await AuthTestAccounts.SeedAsync(
                    this.factory,
                    UserType.Candidate,
                    user => user.Block(DateTimeOffset.UtcNow));
                return this.Authorized("/test-authorization/any", blocked);
        }
    }

    private HttpRequestMessage Authorized(string route, AppUser user)
    {
        string token = this.factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
