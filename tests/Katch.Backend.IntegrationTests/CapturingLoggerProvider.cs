// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Fornecedor de registos para os testes: guarda cada registo com a categoria, o nível, a mensagem e os valores dos
/// âmbitos (<c>BeginScope</c>) ativos nesse momento.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly AsyncLocal<ImmutableStack<object>> scopes = new();

    /// <summary>
    /// Gets the entries logged so far.
    /// </summary>
    public ConcurrentQueue<Entry> Entries { get; } = new();

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(this, categoryName);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nada a libertar.
    }

    /// <summary>
    /// Um registo capturado.
    /// </summary>
    /// <param name="Category">Categoria do registo.</param>
    /// <param name="Level">Nível.</param>
    /// <param name="Message">Mensagem formatada.</param>
    /// <param name="Scope">Valores dos âmbitos ativos.</param>
    internal sealed record Entry(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Scope);

    private sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            ImmutableStack<object> previous = provider.scopes.Value ?? ImmutableStack<object>.Empty;
            provider.scopes.Value = previous.Push(state);
            return new Restore(provider, previous);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            var scope = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (object value in provider.scopes.Value ?? ImmutableStack<object>.Empty)
            {
                if (value is IEnumerable<KeyValuePair<string, object>> pairs)
                {
                    foreach (KeyValuePair<string, object> pair in pairs)
                    {
                        scope.TryAdd(pair.Key, pair.Value);
                    }
                }
            }

            provider.Entries.Enqueue(new Entry(category, logLevel, formatter(state, exception), scope));
        }
    }

    private sealed class Restore(CapturingLoggerProvider provider, ImmutableStack<object> previous) : IDisposable
    {
        public void Dispose()
        {
            provider.scopes.Value = previous;
        }
    }
}
