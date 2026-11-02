// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Candidato na lista do Administrador (documentação da API v02, secções 6.10.2 e 8.5; RF088).
/// </summary>
/// <param name="CandidateId">Identificador do Candidato (o da conta).</param>
/// <param name="FullName">Nome do Candidato.</param>
/// <param name="Email">Correio eletrónico da conta.</param>
/// <param name="LocationName">Designação da localidade, «Localidade (Distrito)».</param>
/// <param name="RegisteredAt">Data e hora do registo (<c>app_user.created_at</c>), em UTC.</param>
/// <param name="Status">Estado da conta: ativa, bloqueada ou suspensa.</param>
public sealed record CandidateAdminDto(
    Guid CandidateId,
    string FullName,
    string Email,
    string LocationName,
    DateTimeOffset RegisteredAt,
    AccountStatus Status);
