// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Vaga publicada na lista global do Administrador (documentação da API v02, secções 6.11.8 e 8.5; RF098).
/// </summary>
/// <param name="JobId">Identificador da vaga.</param>
/// <param name="Title">Função da vaga.</param>
/// <param name="CompanyName">Designação social da Empresa da vaga.</param>
/// <param name="PublishedAt">Data e hora da publicação, em UTC.</param>
/// <param name="InterestCount">Número de interesses na vaga, em todos os estados da decisão.</param>
public sealed record PublishedJobAdminDto(
    Guid JobId,
    string Title,
    string CompanyName,
    DateTimeOffset PublishedAt,
    int InterestCount);
