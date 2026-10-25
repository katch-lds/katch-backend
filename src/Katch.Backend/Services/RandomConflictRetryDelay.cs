// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IConflictRetryDelay"/> com espera aleatória (jitter), de 2 ms até um máximo que cresce
/// com o número da tentativa e tem o teto de 25 ms. A aleatoriedade evita que os pedidos que perderam a corrida voltem
/// a colidir todos ao mesmo tempo.
/// </summary>
public sealed class RandomConflictRetryDelay : IConflictRetryDelay
{
    /// <summary>
    /// Espera mínima, em milissegundos.
    /// </summary>
    internal const int MinMilliseconds = 2;

    /// <summary>
    /// Teto da espera, em milissegundos.
    /// </summary>
    internal const int MaxMilliseconds = 25;

    /// <inheritdoc/>
    public Task WaitAsync(int attempt, CancellationToken cancellationToken = default)
    {
        // O jitter não é criptográfico: serve só para dispersar as repetições.
        int milliseconds = Random.Shared.Next(MinMilliseconds, UpperBound(attempt) + 1);
        return Task.Delay(milliseconds, cancellationToken);
    }

    /// <summary>
    /// Calcula o máximo da espera para a tentativa indicada: 4 ms na primeira, a duplicar até ao teto de 25 ms.
    /// </summary>
    /// <param name="attempt">Número da tentativa que acabou de falhar, a partir de 1.</param>
    /// <returns>O máximo da espera, em milissegundos.</returns>
    internal static int UpperBound(int attempt)
    {
        int exponent = Math.Clamp(attempt, 1, 8);
        return Math.Min(MaxMilliseconds, MinMilliseconds << exponent);
    }
}
