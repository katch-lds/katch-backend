// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Mensagem de uma conversa (documentação da API v02, secções 6.8.2 e 6.8.3; RF026, RF030, RF069).
/// </summary>
/// <param name="MessageId">Identificador da mensagem.</param>
/// <param name="SenderId">Identificador da conta do remetente.</param>
/// <param name="Content">Texto da mensagem.</param>
/// <param name="SentAt">Instante do envio.</param>
/// <param name="ReadAt">Instante em que o destinatário a leu; <see langword="null"/> enquanto por ler.</param>
public sealed record MessageDto(Guid MessageId, Guid SenderId, string Content, DateTimeOffset SentAt, DateTimeOffset? ReadAt);
