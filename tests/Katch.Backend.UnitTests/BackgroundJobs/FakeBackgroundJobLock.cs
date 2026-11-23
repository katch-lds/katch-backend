// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.BackgroundJobs;

namespace Katch.Backend.UnitTests.BackgroundJobs;

/// <summary>
/// Duplo do bloqueio das tarefas periódicas: executa o trabalho, ou, com <see cref="HeldElsewhere"/>, simula outra
/// instância a executá-lo.
/// </summary>
internal sealed class FakeBackgroundJobLock : IBackgroundJobLock
{
    /// <summary>
    /// Gets or sets a value indicating whether another instance holds the lock.
    /// </summary>
    public bool HeldElsewhere { get; set; }

    /// <summary>
    /// Gets the keys requested, one per run.
    /// </summary>
    public List<long> Keys { get; } = [];

    /// <inheritdoc/>
    public async Task<int?> RunExclusiveAsync(long key, Func<Task<int>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        this.Keys.Add(key);
        return this.HeldElsewhere ? null : await work();
    }
}
