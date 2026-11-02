// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Regista, para a matriz de cobertura dos endpoints (I064), o método, o modelo da rota, o código de estado e o teste de
/// origem de cada pedido tratado pela API nos testes de integração. Só escreve quando a variável de ambiente
/// <see cref="FileVariable"/> indica o ficheiro; sem ela, não altera nada nos testes.
/// </summary>
public sealed class EndpointHitRecorder : IStartupFilter
{
    /// <summary>
    /// Nome da variável de ambiente com o caminho do ficheiro de registo (uma linha por pedido, com o método, a rota, o
    /// código de estado e o teste separados por tabulações).
    /// </summary>
    public const string FileVariable = "KATCH_ENDPOINT_HITS";

    /// <summary>
    /// Cabeçalho com o teste que criou o cliente HTTP, acrescentado por <see cref="KatchWebApplicationFactory"/> só
    /// quando o registo está ativo. O servidor de testes não corre no contexto de execução do teste, por isso o teste é
    /// identificado no cliente.
    /// </summary>
    public const string TestHeader = "X-Katch-Test";

    private static readonly Lock Gate = new();

    /// <summary>
    /// Gets a value indicating whether the registo está ativo (variável <see cref="FileVariable"/> definida).
    /// </summary>
    public static bool IsEnabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(FileVariable));

    /// <summary>
    /// Identifica o teste em execução, no formato <c>Classe.Método</c>.
    /// </summary>
    /// <returns>O teste, ou <c>-</c> fora de um teste.</returns>
    public static string CurrentTest()
    {
        string? type = TestContext.Current.TestClass?.TestClassName;
        string? method = TestContext.Current.TestMethod?.MethodName;
        return type is null || method is null ? "-" : string.Concat(type[(type.LastIndexOf('.') + 1)..], ".", method);
    }

    /// <inheritdoc/>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                try
                {
                    await nextMiddleware(context);
                }
                finally
                {
                    Record(context);
                }
            });
            next(app);
        };
    }

    private static void Record(HttpContext context)
    {
        string? file = Environment.GetEnvironmentVariable(FileVariable);
        if (string.IsNullOrEmpty(file))
        {
            return;
        }

        // Sem endpoint (rota inexistente ou restrição :guid não cumprida), fica o caminho pedido.
        string route = context.GetEndpoint() is RouteEndpoint endpoint
            ? "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/')
            : "(sem endpoint) " + context.Request.Path;

        string test = context.Request.Headers[TestHeader].ToString();
        string line = string.Concat(
            context.Request.Method,
            "\t",
            route,
            "\t",
            context.Response.StatusCode.ToString(CultureInfo.InvariantCulture),
            "\t",
            test,
            Environment.NewLine);
        lock (Gate)
        {
            File.AppendAllText(file, line);
        }
    }
}
