// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;

namespace Katch.Backend.UnitTests.Infrastructure;

/// <summary>
/// Testes das respostas de erro do início de sessão em <see cref="ApiErrorResponse"/> (documentação da API v02,
/// secção 4.3: ERR-01 a ERR-05).
/// </summary>
public sealed class ApiErrorResponseTests
{
    /// <summary>
    /// Cada motivo de recusa do início de sessão corresponde ao estado HTTP, ao código e à mensagem fixa do catálogo.
    /// </summary>
    /// <param name="reason">Motivo da recusa.</param>
    /// <param name="status">Estado HTTP esperado.</param>
    /// <param name="code">Código esperado.</param>
    /// <param name="message">Mensagem esperada, exatamente como no catálogo.</param>
    [Theory]
    [InlineData(
        LoginRejection.InvalidCredentials,
        401,
        "ERR-02",
        "O endereço de correio eletrónico ou a palavra-passe estão incorretos.")]
    [InlineData(LoginRejection.AccountBlocked, 403, "ERR-03", "A conta está bloqueada.")]
    [InlineData(LoginRejection.AccountSuspended, 403, "ERR-04", "A conta está suspensa.")]
    [InlineData(LoginRejection.WrongAccessPoint, 403, "ERR-05", "Esta conta não tem acesso por este ponto de acesso.")]
    public void LoginRejectionMapsToTheCatalogError(LoginRejection reason, int status, string code, string message)
    {
        (int statusCode, ApiErrorDto body) = ApiErrorResponse.LoginRejected(reason);

        Assert.Equal(status, statusCode);
        Assert.Equal(code, body.Code);
        Assert.Equal(message, body.Message);
        Assert.Empty(body.Fields);
        Assert.Null(body.BlockedUntil);
    }

    /// <summary>
    /// Um motivo desconhecido nunca produz uma resposta (falha fechada).
    /// </summary>
    [Fact]
    public void UnknownLoginRejectionIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ApiErrorResponse.LoginRejected((LoginRejection)99));
    }

    /// <summary>
    /// O ERR-01 tem a mensagem fixa do catálogo e nenhum campo.
    /// </summary>
    [Fact]
    public void InvalidSessionIsErr01()
    {
        ApiErrorDto body = ApiErrorResponse.InvalidSession();

        Assert.Equal("ERR-01", body.Code);
        Assert.Equal("A sessão expirou ou não é válida. Inicie sessão novamente.", body.Message);
        Assert.Empty(body.Fields);
    }
}
