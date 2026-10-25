// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Errors;

namespace Katch.Backend.UnitTests.Infrastructure.Errors;

/// <summary>
/// Testes dos erros ERR-20, ERR-31, ERR-32, ERR-33 e ERR-41 de <see cref="ApiErrorResponse"/>, com os textos exatos do
/// catálogo da documentação da API v02 (secção 4.3).
/// </summary>
public sealed class ApiErrorResponseJobAnswerTests
{
    /// <summary>
    /// Os erros sem parâmetros têm o código e o texto do catálogo, sem campos e sem <c>blockedUntil</c>.
    /// </summary>
    [Fact]
    public void FixedErrorsMatchTheCatalog()
    {
        AssertError("ERR-20", "O elemento pedido não existe ou não está disponível.", ApiErrorResponse.NotFound());
        AssertError("ERR-32", "Já respondeu a esta vaga.", ApiErrorResponse.AlreadyAnswered());
        AssertError("ERR-33", "A vaga já não está disponível.", ApiErrorResponse.JobUnavailable());
        AssertError(
            "ERR-41",
            "O elemento foi alterado por outro pedido. Atualize e tente novamente.",
            ApiErrorResponse.ConcurrencyConflict());
    }

    /// <summary>
    /// O ERR-31 traz o tempo em falta em horas e minutos, arredondado para cima ao minuto, e o fim do bloqueio.
    /// </summary>
    /// <param name="seconds">Tempo em falta, em segundos.</param>
    /// <param name="expectedTime">Horas e minutos esperados na mensagem.</param>
    [Theory]
    [InlineData(23 * 3600, "23 h 0 min")]
    [InlineData((23 * 3600) + (46 * 60) + 1, "23 h 47 min")]
    [InlineData(24 * 3600, "24 h 0 min")]
    [InlineData(3600, "1 h 0 min")]
    [InlineData(3601, "1 h 1 min")]
    [InlineData(59 * 60, "0 h 59 min")]
    [InlineData(61, "0 h 2 min")]
    [InlineData(60, "0 h 1 min")]
    [InlineData(1, "0 h 1 min")]
    [InlineData(0, "0 h 1 min")]
    [InlineData(-30, "0 h 1 min")]
    public void QuotaExhaustedMessageRoundsMinutesUpWithAMinimumOfOne(int seconds, string expectedTime)
    {
        string message = ApiErrorResponse.QuotaExhaustedMessage(TimeSpan.FromSeconds(seconds));

        Assert.Equal($"Esgotou a quota de interesses. Pode voltar a manifestar interesse dentro de {expectedTime}.", message);
    }

    /// <summary>
    /// O corpo do ERR-31 preenche o <c>blockedUntil</c> com o instante exato do fim do bloqueio.
    /// </summary>
    [Fact]
    public void QuotaExhaustedCarriesBlockedUntil()
    {
        var blockedUntil = new DateTimeOffset(2026, 10, 10, 16, 5, 0, TimeSpan.Zero);

        ApiErrorDto error = ApiErrorResponse.QuotaExhausted(blockedUntil, new TimeSpan(23, 47, 0));

        Assert.Equal("ERR-31", error.Code);
        Assert.Equal(
            "Esgotou a quota de interesses. Pode voltar a manifestar interesse dentro de 23 h 47 min.",
            error.Message);
        Assert.Equal(blockedUntil, error.BlockedUntil);
        Assert.Empty(error.Fields);
    }

    private static void AssertError(string code, string message, ApiErrorDto error)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Empty(error.Fields);
        Assert.Null(error.BlockedUntil);
    }
}
