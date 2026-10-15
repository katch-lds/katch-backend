// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using Katch.Backend.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes do arranque da API: documentação Swagger e contexto da base de dados.
/// </summary>
public sealed class ApplicationStartupTests : IClassFixture<KatchWebApplicationFactory>
{
    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationStartupTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public ApplicationStartupTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// O documento OpenAPI fica disponível no ambiente de desenvolvimento.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SwaggerDocumentIsAvailable()
    {
        using HttpClient client = this.factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/swagger/v1/swagger.json", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// O contexto da base de dados é resolvido e cria o esquema sobre SQLite in-memory.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DbContextCreatesSchemaOnSqliteInMemory()
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();

        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Assert.True(await context.Database.CanConnectAsync(TestContext.Current.CancellationToken));
    }
}
