// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Cartão de vaga da área de exploração (documentação da API v02, secções 6.3.1 e 8.2; RF013). O logótipo e as fotografias
/// só são preenchidos quando estão registados.
/// </summary>
/// <param name="JobId">Identificador da vaga.</param>
/// <param name="Title">Função.</param>
/// <param name="CompanyName">Designação social da Empresa.</param>
/// <param name="LogoUrl">URL do logótipo da Empresa, ou <see langword="null"/> se não tiver logótipo.</param>
/// <param name="MinSalary">Valor mínimo do intervalo salarial, em euros.</param>
/// <param name="MaxSalary">Valor máximo do intervalo salarial, em euros.</param>
/// <param name="LocationName">Localidade da vaga, no formato «Localidade (Distrito)».</param>
/// <param name="DistanceKm">Distância em linha reta à localidade do Candidato, em km arredondados às unidades (RF015).</param>
/// <param name="ContractType">Tipo de contrato.</param>
/// <param name="WorkMode">Regime de trabalho.</param>
/// <param name="IsUrgent">Indicação de urgência.</param>
/// <param name="PhotoUrls">URLs das fotografias da vaga, pela ordem de carregamento (0 a 5).</param>
public sealed record JobCardDto(
    Guid JobId,
    string Title,
    string CompanyName,
    string? LogoUrl,
    decimal MinSalary,
    decimal MaxSalary,
    string LocationName,
    int DistanceKm,
    ContractType ContractType,
    WorkMode WorkMode,
    bool IsUrgent,
    List<string> PhotoUrls);
