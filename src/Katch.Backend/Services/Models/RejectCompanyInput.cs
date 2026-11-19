// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Pedido de recusa do registo de uma Empresa (documentação da API v02, secções 6.11.4 e 8.5; RF094, P12). O texto é
/// anulável para que um motivo em falta chegue ao serviço e seja indicado no erro de campo.
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Reason">Motivo da recusa, com 10 a 500 caracteres sem os espaços nas extremidades.</param>
public sealed record RejectCompanyInput(string? Reason)
{
    /// <summary>
    /// Gets an input without any field, equivalent to an empty request body.
    /// </summary>
    public static RejectCompanyInput Empty { get; } = new((string?)null);
}
