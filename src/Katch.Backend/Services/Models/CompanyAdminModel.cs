// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Services.Models;

/// <summary>
/// Empresa na lista global do Administrador (documentação da API v02, secções 6.11.7 e 8.5; RF097).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="CompanyId">Identificador da Empresa.</param>
/// <param name="CompanyName">Designação social.</param>
/// <param name="Status">Estado da Empresa: pendente, aprovada, recusada ou suspensa.</param>
/// <param name="PublishedJobs">Número de vagas da Empresa no estado publicada.</param>
public sealed record CompanyAdminModel(Guid CompanyId, string CompanyName, CompanyStatus Status, int PublishedJobs);
