// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Uma Empresa com o número das suas vagas publicadas, para a lista global do Administrador (modelo de classes do
/// backend v02, secção 4; RF097).
/// </summary>
/// <param name="Company">Empresa, lida sem acompanhamento do contexto.</param>
/// <param name="PublishedJobs">Número de vagas da Empresa no estado publicada.</param>
public sealed record CompanyWithCount(Company Company, int PublishedJobs);
