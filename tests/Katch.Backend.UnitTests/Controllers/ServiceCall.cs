// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Chamada recebida por um <see cref="ServiceDouble{TService}"/>.
/// </summary>
/// <param name="Method">Nome do método.</param>
/// <param name="Arguments">Argumentos, pela ordem da assinatura (incluindo o token de cancelamento).</param>
public sealed record ServiceCall(string Method, IReadOnlyList<object?> Arguments)
{
    /// <summary>
    /// Devolve o argumento na posição indicada, convertido no tipo esperado.
    /// </summary>
    /// <typeparam name="T">Tipo do argumento.</typeparam>
    /// <param name="index">Posição do argumento.</param>
    /// <returns>O argumento.</returns>
    public T Argument<T>(int index)
    {
        return Assert.IsType<T>(this.Arguments[index], exactMatch: false);
    }
}
