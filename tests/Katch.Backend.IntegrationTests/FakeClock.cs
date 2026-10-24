// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Relógio dos testes: segue o relógio do sistema, a não ser que se fixe um instante em <see cref="Fixed"/>.
/// </summary>
public sealed class FakeClock : IClock
{
    /// <summary>
    /// Gets or sets the instant returned by <see cref="UtcNow"/>; <see langword="null"/> segue o relógio do sistema.
    /// </summary>
    public DateTimeOffset? Fixed { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset UtcNow => this.Fixed ?? DateTimeOffset.UtcNow;
}
