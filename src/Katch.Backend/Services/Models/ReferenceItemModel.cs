// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Elemento de uma lista pré-definida: localidade, competência ou benefício (documentação da API v02, secção 8.3).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Id">Identificador do elemento.</param>
/// <param name="Name">Designação apresentada; nas localidades, «Localidade (Distrito)».</param>
public sealed record ReferenceItemModel(Guid Id, string Name);
