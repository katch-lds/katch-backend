// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Conversa de um utilizador com os dados que a lista apresenta, lidos numa só consulta (documentação da API v02,
/// secção 6.8.1). A data da última mensagem e as mensagens por ler são calculadas a partir de <c>message</c>.
/// </summary>
/// <param name="Match">O match a que a conversa pertence.</param>
/// <param name="CounterpartName">Nome da outra parte, em relação ao utilizador que consulta.</param>
/// <param name="JobTitle">Função da vaga.</param>
/// <param name="CompanyName">Designação da Empresa.</param>
/// <param name="LastMessageAt">Instante da última mensagem; <see langword="null"/> sem mensagens.</param>
/// <param name="UnreadCount">Mensagens da outra parte por ler.</param>
public sealed record ConversationView(
    Match Match,
    string CounterpartName,
    string JobTitle,
    string CompanyName,
    DateTimeOffset? LastMessageAt,
    int UnreadCount);
