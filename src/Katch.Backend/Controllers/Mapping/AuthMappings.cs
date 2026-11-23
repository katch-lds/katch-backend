// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Conversões entre os DTOs da API e os modelos da camada de serviços da autenticação e conta (documentação da API v02,
/// secção 3). O mapeamento fica na camada da API, para que os serviços não dependam dos DTOs (I138).
/// </summary>
public static class AuthMappings
{
    /// <summary>
    /// Converte o pedido da API (<see cref="RegisterCandidateRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static RegisterCandidateInput ToInput(this RegisterCandidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new RegisterCandidateInput(
            request.FullName,
            request.Email,
            request.PhoneNumber,
            request.LocationId,
            request.Password,
            request.AcceptTerms);
    }

    /// <summary>
    /// Converte o pedido da API (<see cref="CreateRecruiterAccountRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static CreateRecruiterAccountInput ToInput(this CreateRecruiterAccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateRecruiterAccountInput(request.Email, request.Password, request.AcceptTerms);
    }

    /// <summary>
    /// Converte o pedido da API (<see cref="LoginRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static LoginInput ToInput(this LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new LoginInput(request.Email, request.Password);
    }

    /// <summary>
    /// Converte o pedido da API (<see cref="ChangePasswordRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static ChangePasswordInput ToInput(this ChangePasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ChangePasswordInput(request.CurrentPassword, request.NewPassword);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="LoginResponse"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static LoginResponse ToDto(this LoginResult model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new LoginResponse(model.Token, model.ExpiresAt, model.UserType, model.UserId);
    }
}
