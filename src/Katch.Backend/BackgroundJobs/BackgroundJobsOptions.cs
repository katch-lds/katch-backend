// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.BackgroundJobs;

/// <summary>
/// Configuração das tarefas periódicas, lida da secção <c>BackgroundJobs</c> (modelo de classes do backend v02,
/// secção 5.2). O intervalo é partilhado pelas duas tarefas (reposição da quota e encerramento das vagas expiradas).
/// </summary>
public sealed class BackgroundJobsOptions
{
    /// <summary>
    /// Nome da secção de configuração.
    /// </summary>
    public const string SectionName = "BackgroundJobs";

    /// <summary>
    /// Intervalo por omissão entre duas execuções, em segundos: as tarefas correm a cada minuto.
    /// </summary>
    public const int DefaultIntervalSeconds = 60;

    /// <summary>
    /// Maior intervalo aceite, em segundos (24 horas, o período de bloqueio da quota). Acima disto a tarefa deixava de
    /// ter utilidade e o temporizador do .NET deixa de aceitar valores muito maiores.
    /// </summary>
    public const int MaxIntervalSeconds = 86_400;

    /// <summary>
    /// Gets or sets the interval between two runs, in seconds, from 1 to <see cref="MaxIntervalSeconds"/>.
    /// </summary>
    public int IntervalSeconds { get; set; } = DefaultIntervalSeconds;
}
