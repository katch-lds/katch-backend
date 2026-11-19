// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Detalhe de uma vaga na área de exploração (documentação da API v02, secções 6.3.2 e 8.2; RF016).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Card">Dados do cartão da vaga, com a distância à localidade do Candidato.</param>
/// <param name="Description">Descrição completa da oportunidade.</param>
/// <param name="Skills">Designações das competências pretendidas, por ordem alfabética.</param>
/// <param name="Benefits">Designações dos benefícios, por ordem alfabética.</param>
/// <param name="CompanyId">Identificador da Empresa, para consultar a página de apresentação (6.3.3).</param>
public sealed record JobDetailModel(
    JobCardModel Card,
    string Description,
    List<string> Skills,
    List<string> Benefits,
    Guid CompanyId);
