// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Pedido de início de sessão (documentação da API v02, secções 6.1.3 e 8.1; modelo de classes do backend v02,
/// secção 7.1). Só se verifica que os campos não estão vazios: o formato do correio não é revelado (RNF009). Os textos
/// são anuláveis para que um campo em falta chegue ao serviço e seja indicado na mesma resposta que o outro.
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Email">Correio eletrónico da conta.</param>
/// <param name="Password">Palavra-passe da conta.</param>
public sealed record LoginInput(string? Email, string? Password)
{
    /// <summary>
    /// Gets an input without any field, equivalent to an empty request body.
    /// </summary>
    public static LoginInput Empty { get; } = new(null, null);
}
