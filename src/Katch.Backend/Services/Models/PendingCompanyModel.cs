// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Empresa pendente de aprovação na lista do Administrador (documentação da API v02, secções 6.11.1 e 8.5; RF091).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="CompanyId">Identificador da Empresa.</param>
/// <param name="CompanyName">Designação social.</param>
/// <param name="TaxId">Número de identificação fiscal.</param>
/// <param name="SubmittedAt">Data e hora da última submissão do pedido, em UTC.</param>
public sealed record PendingCompanyModel(Guid CompanyId, string CompanyName, string TaxId, DateTimeOffset SubmittedAt);
