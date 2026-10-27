// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// Espera entre duas tentativas de uma operação que falhou por conflito de concorrência (<c>xmin</c>). É uma interface
/// para que os testes não esperem de verdade.
/// </summary>
public interface IConflictRetryDelay
{
    /// <summary>
    /// Espera antes da tentativa seguinte.
    /// </summary>
    /// <param name="attempt">Número da tentativa que acabou de falhar, a partir de 1.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando se pode tentar de novo.</returns>
    public Task WaitAsync(int attempt, CancellationToken cancellationToken = default);
}
