// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Match confirmado, na lista do Candidato e na do Recrutador (documentação da API v02, secção 6.7.1; RF024, RF025,
/// RF067).
/// </summary>
/// <param name="MatchId">Identificador do match, que é também o da conversa.</param>
/// <param name="JobTitle">Função da vaga.</param>
/// <param name="JobStatus">Estado atual da vaga; um match de uma vaga encerrada continua na lista.</param>
/// <param name="LocationName">Localidade da vaga, no formato «Localidade (Distrito)».</param>
/// <param name="CompanyName">Designação da Empresa.</param>
/// <param name="LogoUrl">URL do logótipo da Empresa; <see langword="null"/> sem logótipo.</param>
/// <param name="CounterpartName">Nome da outra parte: o responsável da Empresa, para o Candidato; o Candidato, para o Recrutador.</param>
/// <param name="ContactEmail">Correio eletrónico da outra parte.</param>
/// <param name="ContactPhone">Contacto telefónico da outra parte.</param>
/// <param name="MatchedAt">Instante em que o Recrutador aceitou o Candidato.</param>
public sealed record MatchDto(
    Guid MatchId,
    string JobTitle,
    JobStatus JobStatus,
    string LocationName,
    string CompanyName,
    string? LogoUrl,
    string CounterpartName,
    string ContactEmail,
    string ContactPhone,
    DateTimeOffset MatchedAt);
