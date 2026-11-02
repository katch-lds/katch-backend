// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Envio de uma mensagem (documentação da API v02, secção 6.8.3; RF030; P03).
/// </summary>
/// <param name="Content">Texto da mensagem: 1 a 1000 caracteres, não só espaços.</param>
public sealed record SendMessageRequest(string? Content)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty.
    /// </summary>
    public static SendMessageRequest Empty { get; } = new((string?)null);
}
