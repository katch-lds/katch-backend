// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Conversões entre os DTOs da API e os modelos da camada de serviços da avaliação de candidatos (documentação da API
/// v02, secção 6.6). O mapeamento fica na camada da API, para que os serviços não dependam dos DTOs (I138).
/// </summary>
public static class CandidateEvaluationMappings
{
    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="WaitingCandidateDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static WaitingCandidateDto ToDto(this WaitingCandidateModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new WaitingCandidateDto(
            model.MatchId,
            model.CandidateId,
            model.FullName,
            model.DesiredRole,
            model.LocationName,
            model.InterestAt);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="CandidateFullProfileDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static CandidateFullProfileDto ToDto(this CandidateFullProfileModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CandidateFullProfileDto(
            model.MatchId,
            model.FullName,
            model.PhotoUrl,
            model.DesiredRole,
            model.LocationName,
            model.Availability,
            model.ExperienceSummary,
            model.Links,
            model.Skills.Select(item => item.ToDto()).ToList(),
            model.HasCv,
            model.ProfileOpenedAt);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="SkillTagDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static SkillTagDto ToDto(this SkillTagModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new SkillTagDto(model.Name, model.MatchesJob);
    }
}
