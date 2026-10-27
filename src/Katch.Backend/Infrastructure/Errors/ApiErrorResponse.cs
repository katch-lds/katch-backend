// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.Json;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Services;

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
    /// Código do erro de credenciais erradas no início de sessão.
    /// </summary>
    public const string InvalidCredentialsCode = "ERR-02";

    /// <summary>
    /// Código do erro de conta bloqueada.
    /// </summary>
    public const string AccountBlockedCode = "ERR-03";

    /// <summary>
    /// Código do erro de conta suspensa.
    /// </summary>
    public const string AccountSuspendedCode = "ERR-04";

    /// <summary>
    /// Código do erro de tipo de conta que entra pelo ponto de acesso errado.
    /// </summary>
    public const string WrongAccessPointCode = "ERR-05";

    /// <summary>
    /// Código do erro de operação não disponível para o tipo de conta.
    /// </summary>
    public const string AccountTypeNotAllowedCode = "ERR-06";

    /// <summary>
    /// Código do erro de Empresa não aprovada.
    /// </summary>
    public const string CompanyNotApprovedCode = "ERR-07";

    /// <summary>
    /// Código do erro de validação automática dos campos.
    /// </summary>
    public const string ValidationFailedCode = "ERR-10";

    /// <summary>
    /// Código do erro de elemento inexistente ou não disponível.
    /// </summary>
    public const string NotFoundCode = "ERR-20";

    /// <summary>
    /// Código do erro de transição de estado inválida.
    /// </summary>
    public const string InvalidStateCode = "ERR-30";

    /// <summary>
    /// Código do erro de segundo registo de Empresa pelo mesmo Recrutador.
    /// </summary>
    public const string CompanyAlreadyRegisteredCode = "ERR-40";

    /// <summary>
    /// Mensagem do ERR-01.
    /// </summary>
    public const string InvalidSessionMessage = "A sessão expirou ou não é válida. Inicie sessão novamente.";

    /// <summary>
    /// Mensagem do ERR-02, a mesma para o correio inexistente e para a palavra-passe errada (RNF009).
    /// </summary>
    public const string InvalidCredentialsMessage = "O endereço de correio eletrónico ou a palavra-passe estão incorretos.";

    /// <summary>
    /// Mensagem do ERR-03.
    /// </summary>
    public const string AccountBlockedMessage = "A conta está bloqueada.";

    /// <summary>
    /// Mensagem do ERR-04.
    /// </summary>
    public const string AccountSuspendedMessage = "A conta está suspensa.";

    /// <summary>
    /// Mensagem do ERR-05.
    /// </summary>
    public const string WrongAccessPointMessage = "Esta conta não tem acesso por este ponto de acesso.";

    /// <summary>
    /// Mensagem do ERR-06.
    /// </summary>
    public const string AccountTypeNotAllowedMessage = "A operação não está disponível para este tipo de conta.";

    /// <summary>
    /// Mensagem do ERR-10.
    /// </summary>
    public const string ValidationFailedMessage = "Os dados enviados não cumprem a validação automática.";

    /// <summary>
    /// Mensagem do ERR-20.
    /// </summary>
    public const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";

    /// <summary>
    /// Mensagem do ERR-40.
    /// </summary>
    public const string CompanyAlreadyRegisteredMessage = "O Recrutador já registou uma Empresa.";

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

    /// <summary>
    /// Escreve o ERR-07 (403): a operação exige a Empresa aprovada.
    /// </summary>
    /// <param name="context">Contexto do pedido.</param>
    /// <param name="message">Mensagem, obtida com <see cref="CompanyNotApprovedMessage"/>.</param>
    /// <returns>Tarefa que termina quando a resposta está escrita.</returns>
    public static Task WriteCompanyNotApprovedAsync(HttpContext context, string message)
    {
        return WriteAsync(context, StatusCodes.Status403Forbidden, CompanyNotApprovedCode, message);
    }

    /// <summary>
    /// Cria o corpo do erro que corresponde ao motivo da recusa de um início de sessão: ERR-02 (401), ERR-03, ERR-04 ou
    /// ERR-05 (403).
    /// </summary>
    /// <param name="reason">Motivo da recusa.</param>
    /// <returns>O código de estado HTTP e o corpo da resposta.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Se o motivo não for conhecido.</exception>
    public static (int StatusCode, ApiErrorDto Body) LoginRejected(LoginRejection reason)
    {
        return reason switch
        {
            LoginRejection.InvalidCredentials => (
                StatusCodes.Status401Unauthorized,
                new ApiErrorDto(InvalidCredentialsCode, InvalidCredentialsMessage)),
            LoginRejection.AccountBlocked => (
                StatusCodes.Status403Forbidden,
                new ApiErrorDto(AccountBlockedCode, AccountBlockedMessage)),
            LoginRejection.AccountSuspended => (
                StatusCodes.Status403Forbidden,
                new ApiErrorDto(AccountSuspendedCode, AccountSuspendedMessage)),
            LoginRejection.WrongAccessPoint => (
                StatusCodes.Status403Forbidden,
                new ApiErrorDto(WrongAccessPointCode, WrongAccessPointMessage)),
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Motivo de recusa desconhecido."),
        };
    }

    /// <summary>
    /// Cria o corpo do ERR-01 (401): credencial em falta, inválida ou expirada, ou conta inexistente.
    /// </summary>
    /// <returns>O corpo da resposta.</returns>
    public static ApiErrorDto InvalidSession()
    {
        return new ApiErrorDto(InvalidSessionCode, InvalidSessionMessage);
    }

    /// <summary>
    /// Cria o corpo do ERR-10 (400): um ou mais campos não cumprem a validação automática (RF004, RF042, RNF008).
    /// </summary>
    /// <param name="fields">Um erro por campo em causa.</param>
    /// <returns>O corpo da resposta.</returns>
    public static ApiErrorDto ValidationFailed(IReadOnlyList<FieldErrorDto> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return new ApiErrorDto(ValidationFailedCode, ValidationFailedMessage, fields);
    }

    /// <summary>
    /// Cria o corpo do ERR-20 (404): o elemento pedido não existe ou não está disponível.
    /// </summary>
    /// <returns>O corpo da resposta.</returns>
    public static ApiErrorDto NotFound()
    {
        return new ApiErrorDto(NotFoundCode, NotFoundMessage);
    }

    /// <summary>
    /// Cria o corpo do ERR-30 (409): a operação não é permitida no estado atual do elemento.
    /// </summary>
    /// <param name="stateLabel">Estado atual, como é apresentado ao utilizador (por exemplo «pendente»).</param>
    /// <returns>O corpo da resposta, por exemplo «A operação não é permitida no estado atual: pendente.».</returns>
    public static ApiErrorDto InvalidState(string stateLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateLabel);

        return new ApiErrorDto(InvalidStateCode, $"A operação não é permitida no estado atual: {stateLabel}.");
    }

    /// <summary>
    /// Cria o corpo do ERR-40 (409): o Recrutador já registou uma Empresa.
    /// </summary>
    /// <returns>O corpo da resposta.</returns>
    public static ApiErrorDto CompanyAlreadyRegistered()
    {
        return new ApiErrorDto(CompanyAlreadyRegisteredCode, CompanyAlreadyRegisteredMessage);
    }

    /// <summary>
    /// Compõe a mensagem do ERR-07 com o estado atual da Empresa.
    /// </summary>
    /// <param name="status">Estado da Empresa, ou <see langword="null"/> se o Recrutador não tem Empresa.</param>
    /// <returns>A mensagem, por exemplo «A operação exige a Empresa aprovada. Estado atual da Empresa: pendente.».</returns>
    /// <exception cref="ArgumentOutOfRangeException">Se o estado não for conhecido.</exception>
    public static string CompanyNotApprovedMessage(CompanyStatus? status)
    {
        return $"A operação exige a Empresa aprovada. Estado atual da Empresa: {CompanyStatusLabel(status)}.";
    }

    /// <summary>
    /// Designação do estado da Empresa nas mensagens do catálogo (ERR-07 e ERR-30).
    /// </summary>
    /// <param name="status">Estado da Empresa, ou <see langword="null"/> se o Recrutador não tem Empresa.</param>
    /// <returns>«pendente», «aprovada», «recusada», «suspensa» ou «não registada».</returns>
    /// <exception cref="ArgumentOutOfRangeException">Se o estado não for conhecido.</exception>
    public static string CompanyStatusLabel(CompanyStatus? status)
    {
        return status switch
        {
            null => "não registada",
            CompanyStatus.Pending => "pendente",
            CompanyStatus.Approved => "aprovada",
            CompanyStatus.Rejected => "recusada",
            CompanyStatus.Suspended => "suspensa",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Estado de Empresa desconhecido."),
        };
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
