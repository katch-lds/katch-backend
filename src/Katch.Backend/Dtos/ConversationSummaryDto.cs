// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Resumo de uma conversa na lista do utilizador (documentação da API v02, secção 6.8.1; RF027, RF029, RF075).
/// </summary>
/// <param name="MatchId">Identificador do match, que é também o da conversa.</param>
/// <param name="CounterpartName">Nome da outra parte.</param>
/// <param name="JobTitle">Função da vaga.</param>
/// <param name="CompanyName">Designação da Empresa.</param>
/// <param name="Status">Estado da conversa: aberta ou em modo apenas de consulta.</param>
/// <param name="CloseReason">Motivo do encerramento; <see langword="null"/> com a conversa aberta.</param>
/// <param name="ClosedBy">Conta que encerrou a conversa; <see langword="null"/> com a conversa aberta e no encerramento automático.</param>
/// <param name="LastMessageAt">Instante da última mensagem, calculado; <see langword="null"/> sem mensagens.</param>
/// <param name="UnreadCount">Mensagens da outra parte por ler.</param>
public sealed record ConversationSummaryDto(
    Guid MatchId,
    string CounterpartName,
    string JobTitle,
    string CompanyName,
    ConversationStatus Status,
    ConversationCloseReason? CloseReason,
    Guid? ClosedBy,
    DateTimeOffset? LastMessageAt,
    int UnreadCount);
