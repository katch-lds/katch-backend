// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Repositories;

/// <summary>
/// Match confirmado com os dados da vaga, da Empresa e da outra parte que a lista de matches apresenta, lidos numa só
/// consulta (documentação da API v02, secção 6.7.1).
/// </summary>
/// <param name="Match">O match.</param>
/// <param name="JobTitle">Função da vaga.</param>
/// <param name="JobStatus">Estado da vaga.</param>
/// <param name="Location">Localidade da vaga.</param>
/// <param name="CompanyName">Designação da Empresa.</param>
/// <param name="CompanyLogoPath">Caminho do logótipo da Empresa; <see langword="null"/> sem logótipo.</param>
/// <param name="ResponsibleName">Nome do responsável da Empresa.</param>
/// <param name="CompanyContactEmail">Correio eletrónico de registo da Empresa.</param>
/// <param name="CompanyContactPhone">Contacto telefónico de registo da Empresa.</param>
/// <param name="CandidateName">Nome do Candidato.</param>
/// <param name="CandidateEmail">Correio eletrónico da conta do Candidato.</param>
/// <param name="CandidatePhone">Contacto telefónico do Candidato.</param>
public sealed record MatchView(
    Match Match,
    string JobTitle,
    JobStatus JobStatus,
    Location Location,
    string CompanyName,
    string? CompanyLogoPath,
    string ResponsibleName,
    string CompanyContactEmail,
    string CompanyContactPhone,
    string CandidateName,
    string CandidateEmail,
    string CandidatePhone);
