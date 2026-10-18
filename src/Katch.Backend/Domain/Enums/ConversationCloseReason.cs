// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Motivo da passagem da conversa ao modo apenas de consulta. Tipo PostgreSQL <c>conversation_close_reason</c>.
/// </summary>
public enum ConversationCloseReason
{
    /// <summary>
    /// Encerrada por uma das partes.
    /// </summary>
    ClosedByParty,

    /// <summary>
    /// Conta de uma das partes bloqueada ou suspensa.
    /// </summary>
    AccountBlockedOrSuspended,

    /// <summary>
    /// Empresa suspensa.
    /// </summary>
    CompanySuspended,
}
