// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Services;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes de <see cref="RandomConflictRetryDelay"/>: a espera cresce com a tentativa e tem o teto de 25 ms.
/// </summary>
public sealed class RandomConflictRetryDelayTests
{
    /// <summary>
    /// O máximo da espera cresce com a tentativa, nunca passa de 25 ms e é sempre superior ao mínimo de 2 ms.
    /// </summary>
    /// <param name="attempt">Tentativa que acabou de falhar.</param>
    /// <param name="expected">Máximo esperado, em milissegundos.</param>
    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 8)]
    [InlineData(3, 16)]
    [InlineData(4, 25)]
    [InlineData(10, 25)]
    [InlineData(1000, 25)]
    [InlineData(0, 4)]
    public void UpperBoundGrowsWithTheAttemptAndHasACeiling(int attempt, int expected)
    {
        Assert.Equal(expected, RandomConflictRetryDelay.UpperBound(attempt));
    }

    /// <summary>
    /// A espera real termina e respeita o cancelamento do pedido.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WaitCompletesAndHonoursCancellation()
    {
        var delay = new RandomConflictRetryDelay();

        await delay.WaitAsync(1, TestContext.Current.CancellationToken);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delay.WaitAsync(1, cancelled.Token));
    }
}
