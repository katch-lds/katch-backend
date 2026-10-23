// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.Json;
using Katch.Backend.Dtos;

namespace Katch.Backend.Infrastructure.Errors;

/// <summary>
/// Único sítio que escreve uma resposta de erro em <see cref="ApiErrorDto"/> (JSON em camelCase), com as mensagens
/// fixas do catálogo de erros. Um erro novo (por exemplo o ERR-07) acrescenta-se aqui.
/// </summary>
public static class ApiErrorResponse
{
    /// <summary>
    /// Código do erro de sessão em falta, inválida ou expirada.
    /// </summary>
    public const string InvalidSessionCode = "ERR-01";

    /// <summary>
    /// Código do erro de conta bloqueada.
    /// </summary>
    public const string AccountBlockedCode = "ERR-03";

    /// <summary>
    /// Código do erro de conta suspensa.
    /// </summary>
    public const string AccountSuspendedCode = "ERR-04";

    /// <summary>
    /// Código do erro de operação não disponível para o tipo de conta.
    /// </summary>
    public const string AccountTypeNotAllowedCode = "ERR-06";

    /// <summary>
    /// Mensagem do ERR-01.
    /// </summary>
    public const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";

    /// <summary>
    /// Mensagem do ERR-03.
    /// </summary>
    public const string AccountBlockedMessage = "A conta está bloqueada.";

    /// <summary>
    /// Mensagem do ERR-04.
    /// </summary>
    public const string AccountSuspendedMessage = "A conta está suspensa.";

    /// <summary>
    /// Mensagem do ERR-06.
    /// </summary>
    public const string AccountTypeNotAllowedMessage = "A operação não está disponível para este tipo de conta.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Escreve o ERR-01 (401): credencial em falta, mal formada, com assinatura inválida ou expirada, ou conta inexistente.
    /// </summary>
    /// <param name="context">Contexto do pedido.</param>
    /// <returns>Tarefa que termina quando a resposta está escrita.</returns>
    public static Task WriteInvalidSessionAsync(HttpContext context)
    {
        return WriteAsync(context, StatusCodes.Status401Unauthorized, InvalidSessionCode, InvalidSessionMessage);
    }

    /// <summary>
    /// Escreve o ERR-03 (403): conta bloqueada.
    /// </summary>
    /// <param name="context">Contexto do pedido.</param>
    /// <returns>Tarefa que termina quando a resposta está escrita.</returns>
    public static Task WriteAccountBlockedAsync(HttpContext context)
    {
        return WriteAsync(context, StatusCodes.Status403Forbidden, AccountBlockedCode, AccountBlockedMessage);
    }

    /// <summary>
    /// Escreve o ERR-04 (403): conta suspensa.
    /// </summary>
    /// <param name="context">Contexto do pedido.</param>
    /// <returns>Tarefa que termina quando a resposta está escrita.</returns>
    public static Task WriteAccountSuspendedAsync(HttpContext context)
    {
        return WriteAsync(context, StatusCodes.Status403Forbidden, AccountSuspendedCode, AccountSuspendedMessage);
    }

    /// <summary>
    /// Escreve o ERR-06 (403): a política não é cumprida pelo tipo de conta.
    /// </summary>
    /// <param name="context">Contexto do pedido.</param>
    /// <returns>Tarefa que termina quando a resposta está escrita.</returns>
    public static Task WriteAccountTypeNotAllowedAsync(HttpContext context)
    {
        return WriteAsync(context, StatusCodes.Status403Forbidden, AccountTypeNotAllowedCode, AccountTypeNotAllowedMessage);
    }

    private static Task WriteAsync(HttpContext context, int statusCode, string code, string message)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(
            new ApiErrorDto(code, message),
            JsonOptions,
            contentType: "application/json",
            context.RequestAborted);
    }
}
