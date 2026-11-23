// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Pedido para acrescentar ao perfil uma competência da opção «Outro» (documentação da API v02, secções 6.2.10 e 8.2).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Label">Designação com 2 a 40 caracteres, só letras, algarismos, espaços e os símbolos + # . - (P07).</param>
public sealed record AddCustomSkillInput(string? Label)
{
    /// <summary>
    /// Gets an input without the field, equivalent to an empty request body.
    /// </summary>
    public static AddCustomSkillInput Empty { get; } = new((string?)null);
}
