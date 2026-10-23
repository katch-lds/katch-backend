// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// Fonte do instante atual, para que os testes possam controlar o relógio.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current instant, in UTC.
    /// </summary>
    public DateTimeOffset UtcNow { get; }
}
