// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Estado do pedido de registo da Empresa (documentação da API v02, secções 6.4.1 a 6.4.3 e 8.3; modelo de classes do
/// backend v02, secção 7.1).
/// </summary>
/// <param name="CompanyId">Identificador da Empresa.</param>
/// <param name="Status">Estado do pedido: pendente, aprovada, recusada ou suspensa.</param>
/// <param name="RejectionReason">Motivo da recusa, só no estado <see cref="CompanyStatus.Rejected"/>.</param>
/// <param name="SubmittedAt">Data e hora da última submissão do pedido, em UTC.</param>
public sealed record CompanyStatusDto(
    Guid CompanyId,
    CompanyStatus Status,
    string? RejectionReason,
    DateTimeOffset SubmittedAt);
