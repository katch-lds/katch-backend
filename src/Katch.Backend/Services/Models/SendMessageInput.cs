// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Envio de uma mensagem (documentação da API v02, secção 6.8.3; RF030; P03).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Content">Texto da mensagem: 1 a 1000 caracteres, não só espaços.</param>
public sealed record SendMessageInput(string? Content)
{
    /// <summary>
    /// Gets an input without any field, equivalent to an empty request body.
    /// </summary>
    public static SendMessageInput Empty { get; } = new((string?)null);
}
