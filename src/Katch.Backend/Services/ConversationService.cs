// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IConversationService"/> (modelo de classes do backend v02, secção 5). O texto das
/// mensagens só passa do pedido para a base de dados: não é registado nem entra em exceções (RNF017).
/// </summary>
public sealed class ConversationService : IConversationService
{
    private readonly IMatchRepository matches;
    private readonly IMessageRepository messages;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationService"/> class.
    /// </summary>
    /// <param name="matches">Repositório dos matches.</param>
    /// <param name="messages">Repositório das mensagens.</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    /// <param name="clock">Relógio do servidor, a única fonte do instante do envio, da leitura e do encerramento.</param>
    public ConversationService(IMatchRepository matches, IMessageRepository messages, IUnitOfWork unitOfWork, IClock clock)
    {
        this.matches = matches;
        this.messages = messages;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public async Task<List<ConversationSummaryDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        List<ConversationView> views = await this.matches.ListConversationsByUserAsync(userId, cancellationToken);

        // Primeiro as conversas por começar, pela data do match; depois as restantes, pela última mensagem.
        return views
            .Where(view => view.Match.ConversationStatus is not null)
            .OrderBy(view => view.LastMessageAt is null ? 0 : 1)
            .ThenByDescending(view => view.LastMessageAt ?? view.Match.RecruiterActionAt ?? view.Match.CandidateActionAt)
            .Select(view => new ConversationSummaryDto(
                view.Match.Id,
                view.CounterpartName,
                view.JobTitle,
                view.CompanyName,
                view.Match.ConversationStatus!.Value,
                view.Match.ConversationCloseReason,
                view.Match.ConversationClosedById,
                view.LastMessageAt,
                view.UnreadCount))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<List<MessageDto>> OpenAsync(Guid userId, Guid matchId, DateTimeOffset? after, CancellationToken cancellationToken = default)
    {
        await this.GetConversationAsync(userId, matchId, cancellationToken);

        // Marca e só depois lê, para que a resposta já traga o instante de leitura das mensagens agora lidas.
        await this.messages.MarkReadAsync(matchId, userId, this.clock.UtcNow, cancellationToken);
        List<Message> list = await this.messages.ListByMatchAsync(matchId, after, cancellationToken);

        return list.Select(ToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<MessageDto> SendAsync(Guid userId, Guid matchId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Match match = await this.GetConversationAsync(userId, matchId, cancellationToken);

        if (!match.CanSendMessages())
        {
            throw new OperationConflictException(ApiErrorResponse.ConversationReadOnly());
        }

        string content = ValidateContent(request.Content);

        var message = Message.Send(matchId, userId, content, this.clock.UtcNow);
        await this.messages.AddAsync(message, cancellationToken);
        await this.unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(message);
    }

    /// <inheritdoc/>
    public async Task CloseAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default)
    {
        Match match = await this.GetConversationAsync(userId, matchId, cancellationToken);

        if (!match.CanSendMessages())
        {
            throw new OperationConflictException(ApiErrorResponse.ConversationReadOnly());
        }

        match.CloseConversation(userId, ConversationCloseReason.ClosedByParty, this.clock.UtcNow);

        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Outro pedido (um bloqueio ou uma suspensão) encerrou a conversa entretanto: o resultado é o mesmo.
            throw new OperationConflictException(ApiErrorResponse.ConversationReadOnly());
        }
    }

    private static string ValidateContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationFailedException([new FieldErrorDto("content", FieldReasons.Required)]);
        }

        if (content.Length > Message.MaxContentLength)
        {
            throw new ValidationFailedException([new FieldErrorDto("content", FieldReasons.TextTooLong(Message.MaxContentLength))]);
        }

        return content;
    }

    private static MessageDto ToDto(Message message)
    {
        return new MessageDto(message.Id, message.SenderId, message.Content, message.SentAt, message.ReadAt);
    }

    /// <summary>
    /// Obtém o match a que o utilizador pertence: 404 se não existir, 403 se a conta não for uma das partes, 409 ERR-37
    /// se não estiver confirmado.
    /// </summary>
    private async Task<Match> GetConversationAsync(Guid userId, Guid matchId, CancellationToken cancellationToken)
    {
        Match match = await this.matches.GetByIdAsync(matchId, cancellationToken) ?? throw new ElementNotFoundException();

        Guid? recruiterId = await this.matches.GetRecruiterIdAsync(matchId, cancellationToken);
        if (match.CandidateId != userId && recruiterId != userId)
        {
            throw new ElementForbiddenException();
        }

        if (match.Status != MatchStatus.Matched)
        {
            throw new OperationConflictException(ApiErrorResponse.ConversationWithoutMatch());
        }

        return match;
    }
}
