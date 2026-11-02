// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Hiperligações profissionais do Candidato (documentação da API v02, secção 6.2.6). Substitui a lista anterior.
/// </summary>
/// <param name="Urls">De 0 a 3 endereços, pela ordem de apresentação, cada um iniciado por <c>http://</c> ou <c>https://</c>
/// e com até 500 caracteres (P10); a falta do campo é indicada como obrigatória.</param>
public sealed record SetLinksRequest(IReadOnlyList<string>? Urls)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty.
    /// </summary>
    public static SetLinksRequest Empty { get; } = new((IReadOnlyList<string>?)null);
}
