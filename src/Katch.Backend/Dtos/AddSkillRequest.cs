// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Pedido para acrescentar ao perfil uma competência da lista pré-definida (documentação da API v02, secções 6.2.4 e
/// 8.2). O identificador é anulável para que a falta do campo chegue ao serviço e seja indicada como campo obrigatório.
/// </summary>
/// <param name="SkillId">Identificador de uma competência da lista pré-definida, ainda não associada ao perfil.</param>
public sealed record AddSkillRequest(Guid? SkillId)
{
    /// <summary>
    /// Gets a request without the field, used when the request body is empty.
    /// </summary>
    public static AddSkillRequest Empty { get; } = new((Guid?)null);
}
