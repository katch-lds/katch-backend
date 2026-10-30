// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Conversas entre o Candidato e o Recrutador de um match (documentação da API v02, secções 6.8.1 a 6.8.4; modelo de
/// classes do backend v02, secção 5; RF026 a RF030, RF069 a RF075). O conteúdo das mensagens é confidencial (RNF017):
/// nunca entra em registos de diagnóstico nem em mensagens de exceção.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Lista as conversas do utilizador, abertas e em modo apenas de consulta, com as mensagens por ler (6.8.1).
    /// </summary>
    /// <param name="userId">Identificador da conta, da credencial de sessão.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Primeiro as conversas sem mensagens, pela data do match; depois as restantes pela última mensagem.</returns>
    public Task<List<ConversationSummaryDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre a conversa (6.8.2): devolve as mensagens e marca como lidas as da outra parte. Funciona também com a conversa
    /// em modo apenas de consulta.
    /// </summary>
    /// <param name="userId">Identificador da conta, da credencial de sessão.</param>
    /// <param name="matchId">Identificador do match.</param>
    /// <param name="after">Só as mensagens enviadas depois deste instante; <see langword="null"/> para o histórico completo.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As mensagens, da mais antiga para a mais recente.</returns>
    /// <exception cref="ElementNotFoundException">Se o match não existir (ERR-20).</exception>
    /// <exception cref="ElementForbiddenException">Se a conta não for uma das partes (ERR-08).</exception>
    /// <exception cref="OperationConflictException">Se o match não estiver confirmado (ERR-37).</exception>
    public Task<List<MessageDto>> OpenAsync(Guid userId, Guid matchId, DateTimeOffset? after, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia uma mensagem (6.8.3).
    /// </summary>
    /// <param name="userId">Identificador da conta, da credencial de sessão.</param>
    /// <param name="matchId">Identificador do match.</param>
    /// <param name="request">Texto da mensagem.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A mensagem gravada.</returns>
    /// <exception cref="ElementNotFoundException">Se o match não existir (ERR-20).</exception>
    /// <exception cref="ElementForbiddenException">Se a conta não for uma das partes (ERR-08).</exception>
    /// <exception cref="OperationConflictException">Se o match não estiver confirmado (ERR-37) ou a conversa estiver em modo apenas de consulta (ERR-36).</exception>
    /// <exception cref="ValidationFailedException">Se o texto for vazio, só de espaços ou tiver mais de 1000 caracteres (ERR-10).</exception>
    public Task<MessageDto> SendAsync(Guid userId, Guid matchId, SendMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encerra a conversa por iniciativa de uma das partes (6.8.4): passa a modo apenas de consulta para as duas.
    /// </summary>
    /// <param name="userId">Identificador da conta, da credencial de sessão.</param>
    /// <param name="matchId">Identificador do match.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a conversa foi encerrada.</returns>
    /// <exception cref="ElementNotFoundException">Se o match não existir (ERR-20).</exception>
    /// <exception cref="ElementForbiddenException">Se a conta não for uma das partes (ERR-08).</exception>
    /// <exception cref="OperationConflictException">Se o match não estiver confirmado (ERR-37) ou a conversa já estiver encerrada, incluindo a perda na concorrência (ERR-36).</exception>
    public Task CloseAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default);
}
