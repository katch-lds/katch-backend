// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Designação de uma competência ou de um benefício acrescentado ou alterado pelo Administrador (documentação da API
/// v02, secções 6.13.4 a 6.13.7 e 8.3; modelo de classes do backend v02, secção 7). O texto é anulável para que a
/// designação em falta chegue ao serviço e seja indicada com o campo.
/// </summary>
/// <param name="Name">Designação, não vazia, até 100 caracteres e única na lista sem distinção de maiúsculas.</param>
public sealed record ReferenceItemRequest(string? Name)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty.
    /// </summary>
    public static ReferenceItemRequest Empty { get; } = new((string?)null);
}
