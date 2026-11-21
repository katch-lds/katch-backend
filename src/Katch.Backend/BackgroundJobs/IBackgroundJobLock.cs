// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.BackgroundJobs;

/// <summary>
/// Exclusão mútua das tarefas periódicas entre instâncias da API: com várias instâncias, só uma executa cada tarefa
/// em cada intervalo (ROB-007).
/// </summary>
public interface IBackgroundJobLock
{
    /// <summary>
    /// Executa o trabalho se nenhuma outra instância estiver a executar a tarefa com a mesma chave.
    /// </summary>
    /// <param name="key">Chave da tarefa, a mesma em todas as instâncias.</param>
    /// <param name="work">Trabalho, que devolve o número de elementos tratados.</param>
    /// <param name="cancellationToken">Cancelamento da tarefa.</param>
    /// <returns>O resultado do trabalho, ou <see langword="null"/> se outra instância o estiver a executar.</returns>
    public Task<int?> RunExclusiveAsync(long key, Func<Task<int>> work, CancellationToken cancellationToken);
}
