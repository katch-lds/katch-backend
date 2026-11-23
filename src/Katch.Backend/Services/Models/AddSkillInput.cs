// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Pedido para acrescentar ao perfil uma competência da lista pré-definida (documentação da API v02, secções 6.2.4 e
/// 8.2). O identificador é anulável para que a falta do campo chegue ao serviço e seja indicada como campo obrigatório.
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="SkillId">Identificador de uma competência da lista pré-definida, ainda não associada ao perfil.</param>
public sealed record AddSkillInput(Guid? SkillId)
{
    /// <summary>
    /// Gets an input without the field, equivalent to an empty request body.
    /// </summary>
    public static AddSkillInput Empty { get; } = new((Guid?)null);
}
