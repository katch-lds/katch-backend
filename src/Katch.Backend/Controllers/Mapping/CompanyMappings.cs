// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Conversões entre os DTOs da API e os modelos da camada de serviços da registo e página da Empresa (documentação da
/// API v02, secção 6.4). O mapeamento fica na camada da API, para que os serviços não dependam dos DTOs (I138).
/// </summary>
public static class CompanyMappings
{
    /// <summary>
    /// Converte o pedido da API (<see cref="CompanyRegistrationRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static CompanyRegistrationInput ToInput(this CompanyRegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CompanyRegistrationInput(
            request.CompanyName,
            request.TaxId,
            request.Industry,
            request.Address,
            request.LocationId,
            request.ContactEmail,
            request.ContactPhone,
            request.ResponsibleName);
    }

    /// <summary>
    /// Converte o pedido da API (<see cref="CompanyPageRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static CompanyPageInput ToInput(this CompanyPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CompanyPageInput(request.Description, request.Website);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="CompanyStatusDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static CompanyStatusDto ToDto(this CompanyStatusModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CompanyStatusDto(model.CompanyId, model.Status, model.RejectionReason, model.SubmittedAt);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="CompanyRegistrationDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static CompanyRegistrationDto ToDto(this CompanyRegistrationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CompanyRegistrationDto(
            model.CompanyId,
            model.CompanyName,
            model.TaxId,
            model.Industry,
            model.Address,
            model.LocationId,
            model.LocationName,
            model.ContactEmail,
            model.ContactPhone,
            model.ResponsibleName,
            model.SubmittedAt);
    }
}
