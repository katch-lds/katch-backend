// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Uma Empresa com a sua localidade, para a consulta dos dados de registo submetidos (RF092).
/// </summary>
/// <param name="Company">Empresa, lida sem acompanhamento do contexto.</param>
/// <param name="Location">Localidade da Empresa, da lista pré-definida.</param>
public sealed record CompanyWithLocation(Company Company, Location Location);
