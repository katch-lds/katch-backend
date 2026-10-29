// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Empresa pendente de aprovação na lista do Administrador (documentação da API v02, secções 6.11.1 e 8.5; RF091).
/// </summary>
/// <param name="CompanyId">Identificador da Empresa.</param>
/// <param name="CompanyName">Designação social.</param>
/// <param name="TaxId">Número de identificação fiscal.</param>
/// <param name="SubmittedAt">Data e hora da última submissão do pedido, em UTC.</param>
public sealed record PendingCompanyDto(Guid CompanyId, string CompanyName, string TaxId, DateTimeOffset SubmittedAt);
