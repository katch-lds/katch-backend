// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain;

/// <summary>
/// Um interesse foi rejeitado porque a quota está esgotada e o período de bloqueio ainda não terminou (RF020, P11).
/// Traz o fim do bloqueio e o tempo em falta, que o serviço converte no erro <c>ERR-31</c> da API (documentação da API
/// v02, secção 4.3). É um tipo próprio para que o serviço não tenha de tratar como «quota esgotada» qualquer outra
/// <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class QuotaExhaustedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuotaExhaustedException"/> class.
    /// </summary>
    /// <param name="blockedUntil">Instante em que termina o período de bloqueio.</param>
    /// <param name="remaining">Tempo em falta até esse instante, contado do instante do interesse rejeitado.</param>
    public QuotaExhaustedException(DateTimeOffset blockedUntil, TimeSpan remaining)
        : base("A quota de interesses está esgotada até ao fim do período de bloqueio.")
    {
        this.BlockedUntil = blockedUntil;
        this.Remaining = remaining;
    }

    /// <summary>
    /// Gets the instant at which the block period ends (<c>next_swipe_available_at</c>).
    /// </summary>
    public DateTimeOffset BlockedUntil { get; }

    /// <summary>
    /// Gets the time left until <see cref="BlockedUntil"/>, counted from the instant of the rejected interest.
    /// </summary>
    public TimeSpan Remaining { get; }
}
