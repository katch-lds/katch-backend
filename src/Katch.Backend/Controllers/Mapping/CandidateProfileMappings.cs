// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Conversões entre os DTOs da API e os modelos da camada de serviços da perfil do Candidato (documentação da API v02,
/// secção 6.2). O mapeamento fica na camada da API, para que os serviços não dependam dos DTOs (I138).
/// </summary>
public static class CandidateProfileMappings
{
    /// <summary>
    /// Converte o pedido da API (<see cref="UpdateCandidateProfileRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static UpdateCandidateProfileInput ToInput(this UpdateCandidateProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateCandidateProfileInput(
            request.DesiredRole,
            request.LocationId,
            request.Availability,
            request.ExperienceSummary);
    }

    /// <summary>
    /// Converte o pedido da API (<see cref="SetLinksRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static SetLinksInput ToInput(this SetLinksRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SetLinksInput(request.Urls);
    }

    /// <summary>
    /// Converte o pedido da API (<see cref="AddSkillRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static AddSkillInput ToInput(this AddSkillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AddSkillInput(request.SkillId);
    }

    /// <summary>
    /// Converte o pedido da API (<see cref="AddCustomSkillRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static AddCustomSkillInput ToInput(this AddCustomSkillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AddCustomSkillInput(request.Label);
    }

    /// <summary>
    /// Converte o pedido de gravação das preferências de procura (<see cref="UpdatePreferencesRequest"/>) nas
    /// preferências a gravar, com as listas em falta vazias (sem filtro).
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>As preferências.</returns>
    public static SearchPreferencesModel ToPreferences(this UpdatePreferencesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SearchPreferencesModel(
            request.MaxDistanceKm,
            request.MinSalaryExpectation,
            request.WorkModes ?? [],
            request.ContractTypes ?? []);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="CandidateProfileDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static CandidateProfileDto ToDto(this CandidateProfileModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CandidateProfileDto(
            model.CandidateId,
            model.FullName,
            model.Email,
            model.PhoneNumber,
            model.LocationId,
            model.LocationName,
            model.DesiredRole,
            model.Availability,
            model.ExperienceSummary,
            model.PhotoUrl,
            model.HasCv,
            model.Links,
            model.Skills.Select(item => item.ToDto()).ToList(),
            model.CustomSkills.Select(item => item.ToDto()).ToList(),
            model.Preferences.ToDto());
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="SearchPreferencesDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static SearchPreferencesDto ToDto(this SearchPreferencesModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new SearchPreferencesDto(
            model.MaxDistanceKm,
            model.MinSalaryExpectation,
            model.WorkModes,
            model.ContractTypes);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="CustomSkillDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static CustomSkillDto ToDto(this CustomSkillModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CustomSkillDto(model.CustomSkillId, model.Label);
    }
}
