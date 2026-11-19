// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Conversões entre os DTOs da API e os modelos da camada de serviços da listas pré-definidas (documentação da API v02,
/// secção 6.13). O mapeamento fica na camada da API, para que os serviços não dependam dos DTOs (I138).
/// </summary>
public static class ReferenceListMappings
{
    /// <summary>
    /// Converte o pedido da API (<see cref="ReferenceItemRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static ReferenceItemInput ToInput(this ReferenceItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ReferenceItemInput(request.Name);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="ReferenceItemDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static ReferenceItemDto ToDto(this ReferenceItemModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ReferenceItemDto(model.Id, model.Name);
    }
}
