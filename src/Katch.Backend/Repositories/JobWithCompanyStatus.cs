// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Repositories;

/// <summary>
/// Uma vaga com o estado da sua Empresa, para verificar a visibilidade da vaga ao Candidato (RF014).
/// </summary>
/// <param name="Job">Vaga, lida sem acompanhamento do contexto.</param>
/// <param name="CompanyStatus">Estado do registo da Empresa da vaga.</param>
public sealed record JobWithCompanyStatus(Job Job, CompanyStatus CompanyStatus);
