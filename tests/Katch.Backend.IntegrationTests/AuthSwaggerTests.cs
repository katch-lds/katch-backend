// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.Json;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Verifica que o documento OpenAPI gerado pelo Swashbuckle documenta o início e o fim de sessão e a alteração da
/// palavra-passe como a documentação da API v02 (secções 3.1, 3.2 e 6.1.3 a 6.1.5).
/// </summary>
public sealed class AuthSwaggerTests : IClassFixture<KatchWebApplicationFactory>
{
    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthSwaggerTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public AuthSwaggerTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// O início de sessão é anónimo, exige o cabeçalho <c>X-Client-App</c> (<c>mobile</c> ou <c>web</c>) e documenta o 200
    /// com o <c>LoginResponse</c> e os erros 400, 401 e 403.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LoginIsAnonymousWithRequiredClientAppHeaderAndDocumentedResponses()
    {
        JsonElement operation = await this.OperationAsync("/api/auth/login", "post");

        Assert.False(operation.TryGetProperty("security", out _));
        JsonElement header = Assert.Single(operation.GetProperty("parameters").EnumerateArray());
        Assert.Equal("X-Client-App", header.GetProperty("name").GetString());
        Assert.Equal("header", header.GetProperty("in").GetString());
        Assert.True(header.GetProperty("required").GetBoolean());
        Assert.Equal(
            ["mobile", "web"],
            header.GetProperty("schema").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(["200", "400", "401", "403"], ResponseCodes(operation));
        Assert.EndsWith(
            "/LoginResponse",
            operation.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            "/LoginRequest",
            operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// O fim de sessão exige a credencial e documenta o 204 e os erros 401 e 403.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LogoutRequiresBearerAndDocumentsItsResponses()
    {
        JsonElement operation = await this.OperationAsync("/api/auth/logout", "post");

        AssertBearerRequired(operation);
        Assert.Equal(["204", "401", "403"], ResponseCodes(operation));
    }

    /// <summary>
    /// A alteração da palavra-passe exige a credencial, recebe o <c>ChangePasswordRequest</c> e documenta o 204 e os erros
    /// 400, 401 e 403.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ChangePasswordRequiresBearerAndDocumentsItsResponses()
    {
        JsonElement operation = await this.OperationAsync("/api/auth/password", "put");

        AssertBearerRequired(operation);
        Assert.Equal(["204", "400", "401", "403"], ResponseCodes(operation));
        Assert.EndsWith(
            "/ChangePasswordRequest",
            operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// O esquema JWT Bearer está declarado, para o botão «Authorize» do Swagger UI enviar a credencial.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BearerSecuritySchemeIsDeclared()
    {
        JsonElement document = await this.DocumentAsync();

        JsonElement scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());
    }

    /// <summary>
    /// Os endpoints anónimos que já existiam não passam a exigir a credencial no Swagger.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AnonymousEndpointsDoNotRequireBearer()
    {
        JsonElement register = await this.OperationAsync("/api/auth/candidates", "post");
        JsonElement locations = await this.OperationAsync("/api/reference-lists/locations", "get");

        Assert.False(register.TryGetProperty("security", out _));
        Assert.False(locations.TryGetProperty("security", out _));
    }

    private static void AssertBearerRequired(JsonElement operation)
    {
        JsonElement requirement = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.Equal(0, requirement.GetProperty("Bearer").GetArrayLength());
    }

    private static List<string> ResponseCodes(JsonElement operation)
    {
        return operation.GetProperty("responses").EnumerateObject().Select(response => response.Name).Order(StringComparer.Ordinal).ToList();
    }

    private async Task<JsonElement> DocumentAsync()
    {
        using HttpClient client = this.factory.CreateClient();
        string json = await client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> OperationAsync(string path, string method)
    {
        JsonElement document = await this.DocumentAsync();
        return document.GetProperty("paths").GetProperty(path).GetProperty(method);
    }
}
