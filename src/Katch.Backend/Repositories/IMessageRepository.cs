// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Acesso às mensagens das conversas (<c>message</c>) (modelo de classes do backend v02, secção 4): envio, consulta e
/// marcação das mensagens como lidas (I116) e contagem das conversas iniciadas para os indicadores (I118).
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// Acrescenta uma mensagem nova, gravada no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte.
    /// </summary>
    /// <param name="message">Mensagem nova.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a mensagem foi acrescentada.</returns>
    public Task AddAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as mensagens de uma conversa, da mais antiga para a mais recente (RF026, RF030).
    /// </summary>
    /// <param name="matchId">Identificador do match.</param>
    /// <param name="after">Só as mensagens enviadas depois deste instante (exclusive); <see langword="null"/> para o histórico completo.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As mensagens, por ordem de envio.</returns>
    public Task<List<Message>> ListByMatchAsync(Guid matchId, DateTimeOffset? after, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca como lidas, de imediato, as mensagens da outra parte ainda por ler (RF069). As do próprio leitor e as já
    /// lidas não são alteradas.
    /// </summary>
    /// <param name="matchId">Identificador do match.</param>
    /// <param name="readerId">Identificador da conta que lê.</param>
    /// <param name="now">Instante da leitura.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O número de mensagens marcadas.</returns>
    public Task<int> MarkReadAsync(Guid matchId, Guid readerId, DateTimeOffset now, CancellationToken cancellationToken = default);

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
