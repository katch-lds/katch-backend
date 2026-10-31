// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Pedido de recusa do registo de uma Empresa (documentação da API v02, secções 6.11.4 e 8.5; RF094, P12). O texto é
/// anulável para que um motivo em falta chegue ao serviço e seja indicado no erro de campo.
/// </summary>
/// <param name="Reason">Motivo da recusa, com 10 a 500 caracteres sem os espaços nas extremidades.</param>
public sealed record RejectCompanyRequest(string? Reason)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty.
    /// </summary>
    public static RejectCompanyRequest Empty { get; } = new((string?)null);
}
