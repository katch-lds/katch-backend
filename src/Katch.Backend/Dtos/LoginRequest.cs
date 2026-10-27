// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Pedido de início de sessão (documentação da API v02, secções 6.1.3 e 8.1; modelo de classes do backend v02,
/// secção 7.1). Só se verifica que os campos não estão vazios: o formato do correio não é revelado (RNF009). Os textos
/// são anuláveis para que um campo em falta chegue ao serviço e seja indicado na mesma resposta que o outro.
/// </summary>
/// <param name="Email">Correio eletrónico da conta.</param>
/// <param name="Password">Palavra-passe da conta.</param>
public sealed record LoginRequest(string? Email, string? Password)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty.
    /// </summary>
    public static LoginRequest Empty { get; } = new(null, null);
}
