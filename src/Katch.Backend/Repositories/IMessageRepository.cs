// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Repositories;

/// <summary>
/// Acesso às mensagens das conversas (<c>message</c>) (modelo de classes do backend v02, secção 4). As restantes
/// operações do modelo (<c>AddAsync</c>, <c>ListByMatchAsync</c>, <c>MarkReadAsync</c>) são acrescentadas pela Issue
/// das conversas.
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// Conta as conversas cuja primeira mensagem foi enviada no período (RF096). Só usa o identificador da conversa e a
    /// data de envio: o texto das mensagens nunca é lido (RF103).
    /// </summary>
    /// <param name="periodStart">Início do período, inclusive.</param>
    /// <param name="periodEnd">Fim do período, exclusivo.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O número de conversas iniciadas.</returns>
    public Task<int> CountConversationsStartedAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default);
}
