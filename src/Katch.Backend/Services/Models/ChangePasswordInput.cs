// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Pedido de alteração da própria palavra-passe (documentação da API v02, secções 6.1.5 e 8.1; modelo de classes do
/// backend v02, secção 7.2). Os textos são anuláveis para que um campo em falta chegue ao serviço e seja indicado na
/// mesma resposta que o outro.
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="CurrentPassword">Palavra-passe atual, que tem de coincidir com a da conta.</param>
/// <param name="NewPassword">Palavra-passe nova, conforme o P05, até 128 caracteres.</param>
public sealed record ChangePasswordInput(string? CurrentPassword, string? NewPassword)
{
    /// <summary>
    /// Gets an input without any field, equivalent to an empty request body.
    /// </summary>
    public static ChangePasswordInput Empty { get; } = new(null, null);
}
