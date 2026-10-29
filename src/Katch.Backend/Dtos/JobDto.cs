// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Vaga da Empresa do Recrutador (documentação da API v02, secções 6.5 e 8.3; modelo de classes do backend v02,
/// secção 7).
/// </summary>
/// <param name="JobId">Identificador da vaga.</param>
/// <param name="Title">Função.</param>
/// <param name="Description">Descrição.</param>
/// <param name="MinSalary">Valor mínimo do intervalo salarial.</param>
/// <param name="MaxSalary">Valor máximo do intervalo salarial.</param>
/// <param name="LocationId">Localidade.</param>
/// <param name="LocationName">Designação da localidade, «Localidade (Distrito)» (DAPI-08).</param>
/// <param name="ContractType">Tipo de contrato.</param>
/// <param name="WorkMode">Regime de trabalho.</param>
/// <param name="Skills">Competências, por ordem alfabética.</param>
/// <param name="Benefits">Benefícios, por ordem alfabética.</param>
/// <param name="IsUrgent">Indicação de vaga urgente.</param>
/// <param name="ExpiresAt">Data-limite, ou <see langword="null"/>.</param>
/// <param name="Status">Estado da vaga.</param>
/// <param name="PublishedAt">Instante da publicação mais recente, ou <see langword="null"/> se nunca foi publicada.</param>
/// <param name="PhotoUrls">URLs da API das fotografias, pela ordem de carregamento.</param>
/// <param name="WaitingCandidates">Interesses em espera de decisão (RF060).</param>
public sealed record JobDto(
    Guid JobId,
    string Title,
    string Description,
    decimal MinSalary,
    decimal MaxSalary,
    Guid LocationId,
    string LocationName,
    ContractType ContractType,
    WorkMode WorkMode,
    List<ReferenceItemDto> Skills,
    List<ReferenceItemDto> Benefits,
    bool IsUrgent,
    DateTimeOffset? ExpiresAt,
    JobStatus Status,
    DateTimeOffset? PublishedAt,
    List<string> PhotoUrls,
    int WaitingCandidates);
