// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Conversões entre os DTOs da API e os modelos da camada de serviços da supervisão de Empresas e indicadores
/// (documentação da API v02, secções 6.10 e 6.12). O mapeamento fica na camada da API, para que os serviços não
/// dependam dos DTOs (I138).
/// </summary>
public static class AdminMappings
{
    /// <summary>
    /// Converte o pedido da API (<see cref="RejectCompanyRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static RejectCompanyInput ToInput(this RejectCompanyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new RejectCompanyInput(request.Reason);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="PendingCompanyDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static PendingCompanyDto ToDto(this PendingCompanyModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new PendingCompanyDto(model.CompanyId, model.CompanyName, model.TaxId, model.SubmittedAt);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="CompanyAdminDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static CompanyAdminDto ToDto(this CompanyAdminModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CompanyAdminDto(model.CompanyId, model.CompanyName, model.Status, model.PublishedJobs);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="PublishedJobAdminDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static PublishedJobAdminDto ToDto(this PublishedJobAdminModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new PublishedJobAdminDto(
            model.JobId,
            model.Title,
            model.CompanyName,
            model.PublishedAt,
            model.InterestCount);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="IndicatorsDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static IndicatorsDto ToDto(this IndicatorsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new IndicatorsDto(
            model.ActiveCandidates,
            model.ApprovedCompanies,
            model.PendingCompanies,
            model.JobsFirstPublished,
            model.InterestsExpressed,
            model.MatchesConfirmed,
            model.ConversationsStarted);
    }
}
