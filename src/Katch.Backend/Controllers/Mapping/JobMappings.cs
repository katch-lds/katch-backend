// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Conversões entre os DTOs da API e os modelos da camada de serviços da gestão de vagas (documentação da API v02,
/// secção 6.5). O mapeamento fica na camada da API, para que os serviços não dependam dos DTOs (I138).
/// </summary>
public static class JobMappings
{
    /// <summary>
    /// Converte o pedido da API (<see cref="JobRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static JobInput ToInput(this JobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new JobInput(
            request.Title,
            request.Description,
            request.MinSalary,
            request.MaxSalary,
            request.LocationId,
            request.ContractType,
            request.WorkMode,
            request.SkillIds,
            request.BenefitIds,
            request.IsUrgent,
            request.ExpiresAt);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="JobDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static JobDto ToDto(this JobModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new JobDto(
            model.JobId,
            model.Title,
            model.Description,
            model.MinSalary,
            model.MaxSalary,
            model.LocationId,
            model.LocationName,
            model.ContractType,
            model.WorkMode,
            model.Skills.Select(item => item.ToDto()).ToList(),
            model.Benefits.Select(item => item.ToDto()).ToList(),
            model.IsUrgent,
            model.ExpiresAt,
            model.Status,
            model.PublishedAt,
            model.PhotoUrls,
            model.WaitingCandidates);
    }
}
