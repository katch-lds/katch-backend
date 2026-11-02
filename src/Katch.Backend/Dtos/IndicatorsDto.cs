// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Painel de indicadores do período (documentação da API v02, secções 6.12.1 e 8.5; RF096). Os três primeiros referem-se
/// ao estado na data final; os outros quatro contam acontecimentos no período. Só tem contagens: nenhum texto de
/// mensagens nem dado pessoal (RF103).
/// </summary>
/// <param name="ActiveCandidates">Candidatos com a conta ativa na data final.</param>
/// <param name="ApprovedCompanies">Empresas aprovadas na data final.</param>
/// <param name="PendingCompanies">Empresas pendentes na data final.</param>
/// <param name="JobsFirstPublished">Vagas publicadas pela primeira vez no período.</param>
/// <param name="InterestsExpressed">Interesses manifestados no período.</param>
/// <param name="MatchesConfirmed">Matches confirmados no período.</param>
/// <param name="ConversationsStarted">Conversas cuja primeira mensagem foi enviada no período.</param>
public sealed record IndicatorsDto(
    int ActiveCandidates,
    int ApprovedCompanies,
    int PendingCompanies,
    int JobsFirstPublished,
    int InterestsExpressed,
    int MatchesConfirmed,
    int ConversationsStarted);
