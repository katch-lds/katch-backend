// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Conversões entre os DTOs da API e os modelos da camada de serviços da administração de contas (documentação da API
/// v02, secção 6.11). O mapeamento fica na camada da API, para que os serviços não dependam dos DTOs (I138).
/// </summary>
public static class AccountMappings
{
    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="AccountDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static AccountDto ToDto(this AccountModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new AccountDto(model.UserId, model.Email, model.UserType, model.Status);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="CandidateAdminDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static CandidateAdminDto ToDto(this CandidateAdminModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CandidateAdminDto(
            model.CandidateId,
            model.FullName,
            model.Email,
            model.LocationName,
            model.RegisteredAt,
            model.Status);
    }
}
