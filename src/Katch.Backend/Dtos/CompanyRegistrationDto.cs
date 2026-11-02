// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Dados de registo submetidos pela Empresa, para o Administrador decidir (documentação da API v02, secções 6.11.2 e 8.5;
/// RF092).
/// </summary>
/// <param name="CompanyId">Identificador da Empresa.</param>
/// <param name="CompanyName">Designação social.</param>
/// <param name="TaxId">Número de identificação fiscal.</param>
/// <param name="Industry">Setor de atividade.</param>
/// <param name="Address">Morada.</param>
/// <param name="LocationId">Identificador da localidade.</param>
/// <param name="LocationName">Designação da localidade, no formato «Localidade (Distrito)».</param>
/// <param name="ContactEmail">Correio eletrónico de contacto.</param>
/// <param name="ContactPhone">Telefone de contacto.</param>
/// <param name="ResponsibleName">Nome do responsável da Empresa.</param>
/// <param name="SubmittedAt">Data e hora da última submissão do pedido, em UTC.</param>
public sealed record CompanyRegistrationDto(
    Guid CompanyId,
    string CompanyName,
    string TaxId,
    Industry Industry,
    string Address,
    Guid LocationId,
    string LocationName,
    string ContactEmail,
    string ContactPhone,
    string ResponsibleName,
    DateTimeOffset SubmittedAt);
