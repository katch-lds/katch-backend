// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Conversões entre os DTOs da API e os modelos da camada de serviços da exploração de vagas (documentação da API v02,
/// secção 6.3). O mapeamento fica na camada da API, para que os serviços não dependam dos DTOs (I138).
/// </summary>
public static class JobExplorationMappings
{
    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="JobCardDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static JobCardDto ToDto(this JobCardModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new JobCardDto(
            model.JobId,
            model.Title,
            model.CompanyName,
            model.LogoUrl,
            model.MinSalary,
            model.MaxSalary,
            model.LocationName,
            model.DistanceKm,
            model.ContractType,
            model.WorkMode,
            model.IsUrgent,
            model.PhotoUrls);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="JobDetailDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static JobDetailDto ToDto(this JobDetailModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new JobDetailDto(model.Card.ToDto(), model.Description, model.Skills, model.Benefits, model.CompanyId);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="QuotaDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static QuotaDto ToDto(this QuotaModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new QuotaDto(model.Available, model.Total, model.BlockedUntil);
    }
}
