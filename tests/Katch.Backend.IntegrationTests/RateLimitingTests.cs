// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes do limite de pedidos (SEG-001): por IP no início de sessão, nos registos e nas localidades, e por conta nos
/// pedidos autenticados. Cada teste usa uma aplicação própria com dois pedidos por janela.
/// </summary>
public sealed class RateLimitingTests
{
    private const int Permits = 2;

    /// <summary>
    /// O terceiro início de sessão do mesmo IP na janela recebe 429 ERR-14, com o tempo de espera em <c>Retry-After</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LoginAboveTheLimitIs429Err14WithRetryAfter()
    {
        using var factory = new KatchWebApplicationFactory { RateLimitPermits = Permits };
        await AuthTestAccounts.SeedAsync(factory, UserType.Admin);
        using HttpClient client = factory.CreateClient();

        for (int i = 0; i < Permits; i++)
        {
            using HttpResponseMessage allowed = await SendLoginAsync(client, forwardedFor: null);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using HttpResponseMessage rejected = await SendLoginAsync(client, forwardedFor: null);

        await AssertTooManyRequestsAsync(rejected);
        Assert.True(int.Parse(rejected.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture) > 0);
    }

    /// <summary>
    /// A contagem do início de sessão é por IP do cliente: outro IP, visto através do proxy, continua a ser atendido.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LoginLimitIsPerClientIp()
    {
        using var isolated = new KatchWebApplicationFactory { RateLimitPermits = Permits };
        await AuthTestAccounts.SeedAsync(isolated, UserType.Admin);
        using WebApplicationFactory<Program> factory = isolated.WithWebHostBuilder(builder =>
            builder.UseSetting(HostingServiceCollectionExtensions.ForwardedHeadersEnabledKey, "true"));
        using HttpClient client = factory.CreateClient();

        for (int i = 0; i <= Permits; i++)
        {
            using HttpResponseMessage first = await SendLoginAsync(client, "203.0.113.10");
        }

        using HttpResponseMessage sameIp = await SendLoginAsync(client, "203.0.113.10");
        using HttpResponseMessage otherIp = await SendLoginAsync(client, "203.0.113.20");

        Assert.Equal(HttpStatusCode.TooManyRequests, sameIp.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherIp.StatusCode);
    }

    /// <summary>
    /// Os dois registos (Candidato e Recrutador) partilham a mesma contagem por IP.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RegistrationsShareTheLimit()
    {
        using var factory = new KatchWebApplicationFactory { RateLimitPermits = Permits };
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage candidate = await RegisterAsync(client, "candidates");
        using HttpResponseMessage recruiter = await RegisterAsync(client, "recruiters");
        using HttpResponseMessage candidateRejected = await RegisterAsync(client, "candidates");
        using HttpResponseMessage recruiterRejected = await RegisterAsync(client, "recruiters");

        Assert.Equal(HttpStatusCode.BadRequest, candidate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, recruiter.StatusCode);
        await AssertTooManyRequestsAsync(candidateRejected);
        await AssertTooManyRequestsAsync(recruiterRejected);
    }

    /// <summary>
    /// A lista anónima das localidades também tem limite por IP.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LocationsAboveTheLimitAre429()
    {
        using var factory = new KatchWebApplicationFactory { RateLimitPermits = Permits };
        await AuthTestAccounts.SeedAsync(factory, UserType.Admin);
        using HttpClient client = factory.CreateClient();
        var route = new Uri("/api/reference-lists/locations", UriKind.Relative);

        for (int i = 0; i < Permits; i++)
        {
            using HttpResponseMessage allowed = await client.GetAsync(route, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using HttpResponseMessage rejected = await client.GetAsync(route, TestContext.Current.CancellationToken);

        await AssertTooManyRequestsAsync(rejected);
    }

    /// <summary>
    /// Os pedidos autenticados contam por conta: a conta que passou o limite recebe 429 e outra conta não é afetada.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AuthenticatedLimitIsPerAccount()
    {
        using var factory = new KatchWebApplicationFactory { RateLimitPermits = Permits };
        string first = await TokenAsync(factory);
        string second = await TokenAsync(factory);
        using HttpClient client = factory.CreateClient();

        for (int i = 0; i < Permits; i++)
        {
            using HttpResponseMessage allowed = await GetAnyAsync(client, first);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using HttpResponseMessage rejected = await GetAnyAsync(client, first);
        using HttpResponseMessage other = await GetAnyAsync(client, second);

        await AssertTooManyRequestsAsync(rejected);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendLoginAsync(HttpClient client, string? forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(AuthTestAccounts.LoginRoute, UriKind.Relative))
        {
            Content = JsonContent.Create(AuthTestAccounts.Credentials("ninguem@exemplo.pt", "errada2026")),
        };
        request.Headers.Add("X-Client-App", "web");
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string accountType)
    {
        return client.PostAsJsonAsync(
            new Uri($"/api/auth/{accountType}", UriKind.Relative),
            new { },
            TestContext.Current.CancellationToken);
    }

    private static async Task<string> TokenAsync(KatchWebApplicationFactory factory)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(factory, UserType.Admin);
        return factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
    }

    private static async Task<HttpResponseMessage> GetAnyAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/test-authorization/any", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertTooManyRequestsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-14", error.GetProperty("code").GetString());
        Assert.Equal(
            "Demasiados pedidos. Tente novamente dentro de instantes.",
            error.GetProperty("message").GetString());
    }
}
