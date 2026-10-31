// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Regista, para a matriz de cobertura dos endpoints (I064), o método, o modelo da rota e o código de estado de cada
/// pedido tratado pela API nos testes de integração. Só escreve quando a variável de ambiente
/// <see cref="FileVariable"/> indica o ficheiro; sem ela, não altera nada nos testes.
/// </summary>
public sealed class EndpointHitRecorder : IStartupFilter
{
    /// <summary>
    /// Nome da variável de ambiente com o caminho do ficheiro de registo (uma linha por pedido, separada por tabulações).
    /// </summary>
    public const string FileVariable = "KATCH_ENDPOINT_HITS";

    private static readonly Lock Gate = new();

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
        string line = string.Concat(
            context.Request.Method,
            "\t",
            route,
            "\t",
            context.Response.StatusCode.ToString(CultureInfo.InvariantCulture),
            Environment.NewLine);
        lock (Gate)
        {
            File.AppendAllText(file, line);
        }
    }
}
