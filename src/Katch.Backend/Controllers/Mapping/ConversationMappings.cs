// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Conversões entre os DTOs da API e os modelos da camada de serviços da matches e conversas (documentação da API v02,
/// secções 6.7 e 6.8). O mapeamento fica na camada da API, para que os serviços não dependam dos DTOs (I138).
/// </summary>
public static class ConversationMappings
{
    /// <summary>
    /// Converte o pedido da API (<see cref="SendMessageRequest"/>) no modelo de entrada da camada de serviços.
    /// </summary>
    /// <param name="request">Pedido recebido.</param>
    /// <returns>O modelo de entrada, com os mesmos valores.</returns>
    public static SendMessageInput ToInput(this SendMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SendMessageInput(request.Content);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="MatchDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static MatchDto ToDto(this MatchModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new MatchDto(
            model.MatchId,
            model.JobTitle,
            model.JobStatus,
            model.LocationName,
            model.CompanyName,
            model.LogoUrl,
            model.CounterpartName,
            model.ContactEmail,
            model.ContactPhone,
            model.MatchedAt);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="MessageDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static MessageDto ToDto(this MessageModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new MessageDto(model.MessageId, model.SenderId, model.Content, model.SentAt, model.ReadAt);
    }

    /// <summary>
    /// Converte o modelo da camada de serviços no DTO da resposta (<see cref="ConversationSummaryDto"/>).
    /// </summary>
    /// <param name="model">Modelo devolvido pelo serviço.</param>
    /// <returns>O DTO, com os mesmos valores.</returns>
    public static ConversationSummaryDto ToDto(this ConversationSummaryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ConversationSummaryDto(
            model.MatchId,
            model.CounterpartName,
            model.JobTitle,
            model.CompanyName,
            model.Status,
            model.CloseReason,
            model.ClosedBy,
            model.LastMessageAt,
            model.UnreadCount);
    }
}
