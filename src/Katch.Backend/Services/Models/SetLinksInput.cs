// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Hiperligações profissionais do Candidato (documentação da API v02, secção 6.2.6). Substitui a lista anterior.
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Urls">De 0 a 3 endereços, pela ordem de apresentação, cada um iniciado por <c>http://</c> ou <c>https://</c>
/// e com até 500 caracteres (P10); a falta do campo é indicada como obrigatória.</param>
public sealed record SetLinksInput(IReadOnlyList<string>? Urls)
{
    /// <summary>
    /// Gets an input without any field, equivalent to an empty request body.
    /// </summary>
    public static SetLinksInput Empty { get; } = new((IReadOnlyList<string>?)null);
}
