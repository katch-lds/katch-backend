// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
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
    public async Task AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await this.context.Messages.AddAsync(message, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<Message>> ListByMatchAsync(Guid matchId, DateTimeOffset? after, CancellationToken cancellationToken = default)
    {
        IQueryable<Message> messages = this.context.Messages
            .AsNoTracking()
            .Where(message => message.MatchId == matchId);

        if (after is not null)
        {
            DateTimeOffset threshold = after.Value;
            messages = messages.Where(message => message.SentAt > threshold);
        }

        return messages
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> MarkReadAsync(Guid matchId, Guid readerId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return this.context.Messages
            .Where(message => message.MatchId == matchId && message.SenderId != readerId && message.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.ReadAt, now), cancellationToken);
    }
}
