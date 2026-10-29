// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="IMessageRepository"/> sobre o <see cref="KatchDbContext"/>.
/// </summary>
public sealed class MessageRepository : IMessageRepository
{
    private readonly KatchDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRepository"/> class.
    /// </summary>
    /// <param name="context">Contexto da base de dados do pedido.</param>
    public MessageRepository(KatchDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public Task<int> CountConversationsStartedAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
    {
        // Uma conversa começa no período se tem mensagens no período e nenhuma antes dele.
        return this.context.Messages
            .Where(message => message.SentAt >= periodStart && message.SentAt < periodEnd)
            .Select(message => message.MatchId)
            .Distinct()
            .CountAsync(
                matchId => !this.context.Messages.Any(earlier => earlier.MatchId == matchId && earlier.SentAt < periodStart),
                cancellationToken);
    }
}
