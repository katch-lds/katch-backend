// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Estado da conversa de um match. Tipo PostgreSQL <c>conversation_status</c>.
/// </summary>
public enum ConversationStatus
{
    /// <summary>
    /// Aberta.
    /// </summary>
    Open,

    /// <summary>
    /// Em modo apenas de consulta.
    /// </summary>
    Closed,
}
