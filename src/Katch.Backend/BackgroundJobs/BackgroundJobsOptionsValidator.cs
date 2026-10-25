// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.Extensions.Options;

namespace Katch.Backend.BackgroundJobs;

/// <summary>
/// Valida a configuração das tarefas periódicas no arranque, para que um valor inválido pare a aplicação com uma
/// mensagem clara e não só quando a tarefa tenta correr. A mensagem não inclui o valor lido.
/// </summary>
internal sealed class BackgroundJobsOptionsValidator : IValidateOptions<BackgroundJobsOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, BackgroundJobsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.IntervalSeconds < 1 || options.IntervalSeconds > BackgroundJobsOptions.MaxIntervalSeconds)
        {
            return ValidateOptionsResult.Fail(
                $"O intervalo das tarefas periódicas tem de estar entre 1 e {BackgroundJobsOptions.MaxIntervalSeconds} "
                + "segundos. Corrija a variável de ambiente BackgroundJobs__IntervalSeconds "
                + $"(configuração BackgroundJobs:IntervalSeconds); por omissão são {BackgroundJobsOptions.DefaultIntervalSeconds} segundos.");
        }

        return ValidateOptionsResult.Success;
    }
}
