// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Authentication;

namespace Katch.Backend.Infrastructure.Hosting;

/// <summary>
/// Registo de segurança de cada pedido (ROB-002): dá ao pedido um identificador de correlação, devolvido no cabeçalho
/// <see cref="CorrelationHeader"/> e presente em todos os registos do pedido (incluindo o da exceção de um 500), e regista
/// as respostas 401, 403 e 429 (inícios de sessão falhados, acessos negados e limites de pedidos). Não regista dados
/// pessoais: nem correio, nem IP, nem corpo, nem consulta; só o método, o caminho, o estado e o identificador da conta.
/// </summary>
internal sealed partial class RequestLoggingMiddleware
{
    /// <summary>
    /// Cabeçalho da resposta com o identificador de correlação do pedido.
    /// </summary>
    internal const string CorrelationHeader = "X-Correlation-Id";

    private readonly RequestDelegate next;
    private readonly ILogger<RequestLoggingMiddleware> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">Passo seguinte do pipeline.</param>
    /// <param name="logger">Registo de segurança.</param>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    /// <summary>
    /// Executa o pedido dentro do âmbito de registo com a correlação e regista a recusa, se a houver.
    /// </summary>
    /// <param name="context">Contexto do pedido.</param>
    /// <returns>Tarefa que termina quando o pedido foi tratado.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId = context.TraceIdentifier;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (this.logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await this.next(context);

            int status = context.Response.StatusCode;
            if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden or StatusCodes.Status429TooManyRequests)
            {
                string account = context.User.TryGetAccountId(out Guid accountId) ? accountId.ToString() : "-";
                this.LogRejected(context.Request.Method, context.Request.Path.Value ?? string.Empty, status, account);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pedido recusado: {Method} {Path} -> {StatusCode} (conta {AccountId}).")]
    private partial void LogRejected(string method, string path, int statusCode, string accountId);
}
