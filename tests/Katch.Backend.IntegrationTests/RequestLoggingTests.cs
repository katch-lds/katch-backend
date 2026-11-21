// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Json;
using Katch.Backend.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes do registo de segurança dos pedidos (ROB-002): recusas registadas sem dados pessoais e correlação comum à
/// resposta e aos registos do pedido.
/// </summary>
public sealed class RequestLoggingTests : IDisposable
{
    private const string MiddlewareCategory = "Katch.Backend.Infrastructure.Hosting.RequestLoggingMiddleware";

    private readonly KatchWebApplicationFactory isolated = new();
    private readonly CapturingLoggerProvider logs = new();
    private readonly WebApplicationFactory<Program> factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLoggingTests"/> class.
    /// </summary>
    public RequestLoggingTests()
    {
        this.factory = this.isolated.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(this.logs)));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.factory.Dispose();
        this.isolated.Dispose();
    }

    /// <summary>
    /// Um início de sessão falhado fica registado com o caminho e o estado, sem o correio nem a palavra-passe, e com a
    /// correlação devolvida na resposta.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task FailedLoginIsLoggedWithoutPersonalData()
    {
        await AuthTestAccounts.SeedAsync(this.isolated, UserType.Admin);
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(AuthTestAccounts.LoginRoute, UriKind.Relative))
        {
            Content = JsonContent.Create(AuthTestAccounts.Credentials("pessoa.privada@exemplo.pt", "Segredo-2026")),
        };
        request.Headers.Add("X-Client-App", "web");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        string correlation = response.Headers.GetValues("X-Correlation-Id").Single();
        CapturingLoggerProvider.Entry entry = Assert.Single(this.logs.Entries, item => item.Category == MiddlewareCategory);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("POST /api/auth/login -> 401", entry.Message, StringComparison.Ordinal);
        Assert.Equal(correlation, entry.Scope["CorrelationId"]);
        Assert.DoesNotContain(
            this.logs.Entries,
            item => item.Message.Contains("pessoa.privada", StringComparison.Ordinal)
                || item.Message.Contains("Segredo-2026", StringComparison.Ordinal));
    }

    /// <summary>
    /// O registo da exceção de um 500 tem a mesma correlação que a resposta.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnexpectedErrorLogCarriesTheCorrelation()
    {
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/test-request-info/failure", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        string correlation = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.Contains(
            this.logs.Entries,
            item => item.Level == LogLevel.Error && Equals(item.Scope.GetValueOrDefault("CorrelationId"), correlation));
    }

    /// <summary>
    /// Os pedidos atendidos não geram registos de recusa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SuccessfulRequestsAreNotLoggedAsRejected()
    {
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/health", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        Assert.DoesNotContain(this.logs.Entries, item => item.Category == MiddlewareCategory);
    }
}
