// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Pedido para acrescentar ao perfil uma competência da opção «Outro» (documentação da API v02, secções 6.2.10 e 8.2).
/// </summary>
/// <param name="Label">Designação com 2 a 40 caracteres, só letras, algarismos, espaços e os símbolos + # . - (P07).</param>
public sealed record AddCustomSkillRequest(string? Label)
{
    /// <summary>
    /// Gets a request without the field, used when the request body is empty.
    /// </summary>
    public static AddCustomSkillRequest Empty { get; } = new((string?)null);
}
