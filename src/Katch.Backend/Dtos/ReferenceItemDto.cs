// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Elemento de uma lista pré-definida: localidade, competência ou benefício (documentação da API v02, secção 8.3).
/// </summary>
/// <param name="Id">Identificador do elemento.</param>
/// <param name="Name">Designação apresentada; nas localidades, «Localidade (Distrito)».</param>
public sealed record ReferenceItemDto(Guid Id, string Name);
