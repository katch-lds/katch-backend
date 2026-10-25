// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Repositories;

/// <summary>
/// A gravação falhou porque outro pedido alterou a mesma linha depois de ela ter sido lida: a marca de concorrência
/// otimista <c>xmin</c> já não coincide (modelo de dados v02, secção 7.3).
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class.
    /// </summary>
    /// <param name="innerException">Exceção original do Entity Framework Core.</param>
    public ConcurrencyConflictException(Exception innerException)
        : base("A linha foi alterada por outro pedido depois de ser lida.", innerException)
    {
    }
}
