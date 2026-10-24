// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Domain;

/// <summary>
/// Resposta de um Candidato a uma vaga (tabela <c>match</c>): recusa ou interesse, abertura do perfil pelo Recrutador,
/// decisão do Recrutador, match confirmado e a conversa associada. O identificador é também o identificador da conversa.
/// </summary>
/// <remarks>
/// O estado é único (<see cref="Status"/>) e só admite as quatro combinações do diagrama de estados do interesse e do
/// match (modelo de dados v02, secção 6.13 e DM-21; modelo de classes do backend v02, secção 3.4): T1 (recusa da vaga),
/// T2 (interesse em espera), T3 (abertura do perfil), T4 (decisão sem perfil aberto, rejeitada), T5 (aceitação) e T6
/// (recusa do Candidato). As operações aplicam as regras que dependem só desta linha e correspondem aos
/// <c>CHECK</c> da tabela. A quota de interesses, a visibilidade da vaga, a titularidade do Recrutador, as notificações
/// e a transação da aceitação dependem de outras linhas e ficam nos serviços. A transição T7 (encerramento dos
/// interesses em espera sem decisão) não está ratificada e não é implementada (RF113). A concorrência é controlada por
/// <see cref="Version"/>, que corresponde à coluna de sistema <c>xmin</c> do PostgreSQL (secção 7.3).
/// </remarks>
public sealed class Match
{
    private Match(Guid candidateId, Guid jobId, MatchStatus status, DateTimeOffset now)
    {
        if (candidateId == Guid.Empty)
        {
            throw new ArgumentException("O identificador do Candidato é obrigatório.", nameof(candidateId));
        }

        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("O identificador da vaga é obrigatório.", nameof(jobId));
        }

        this.Id = Guid.NewGuid();
        this.CandidateId = candidateId;
        this.JobId = jobId;
        this.Status = status;
        this.CandidateActionAt = now;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Match"/> class. Usado pelo Entity Framework Core.
    /// </summary>
    private Match()
    {
    }

    /// <summary>
    /// Gets the identifier of the match (<c>id</c>), which also identifies the conversation of the match.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the identifier of the candidate that answered the job (<c>candidate_id</c>).
    /// </summary>
    public Guid CandidateId { get; private set; }

    /// <summary>
    /// Gets the identifier of the job (<c>job_id</c>).
    /// </summary>
    public Guid JobId { get; private set; }

    /// <summary>
    /// Gets the state of the answer and of the match (<c>status</c>).
    /// </summary>
    public MatchStatus Status { get; private set; }

    /// <summary>
    /// Gets the instant of the interest or of the decline of the job by the candidate (<c>candidate_action_at</c>).
    /// </summary>
    public DateTimeOffset CandidateActionAt { get; private set; }

    /// <summary>
    /// Gets the instant of the first opening of the full profile for this job (<c>profile_opened_at</c>).
    /// </summary>
    public DateTimeOffset? ProfileOpenedAt { get; private set; }

    /// <summary>
    /// Gets the instant of the decision of the recruiter (<c>recruiter_action_at</c>), which is also the instant in which
    /// the match is confirmed.
    /// </summary>
    public DateTimeOffset? RecruiterActionAt { get; private set; }

    /// <summary>
    /// Gets the identifier of the recruiter that decided (<c>decided_by</c>).
    /// </summary>
    public Guid? DecidedById { get; private set; }

    /// <summary>
    /// Gets the state of the conversation (<c>conversation_status</c>), filled in only when the match is confirmed.
    /// </summary>
    public ConversationStatus? ConversationStatus { get; private set; }

    /// <summary>
    /// Gets the instant in which the conversation became read-only (<c>conversation_closed_at</c>).
    /// </summary>
    public DateTimeOffset? ConversationClosedAt { get; private set; }

    /// <summary>
    /// Gets the identifier of the account that closed the conversation (<c>conversation_closed_by</c>); empty when the
    /// closing was automatic.
    /// </summary>
    public Guid? ConversationClosedById { get; private set; }

    /// <summary>
    /// Gets the reason why the conversation became read-only (<c>conversation_close_reason</c>), kept after any
    /// reactivation.
    /// </summary>
    public ConversationCloseReason? ConversationCloseReason { get; private set; }

    /// <summary>
    /// Gets the optimistic concurrency token, mapped to the <c>xmin</c> system column of PostgreSQL.
    /// </summary>
    public uint Version { get; private set; }

    /// <summary>
    /// Regista a recusa da vaga pelo Candidato (RF018, T1), no estado <see cref="MatchStatus.DeclinedByCandidate"/>.
    /// Não consome a quota.
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="now">Instante da recusa, em UTC.</param>
    /// <returns>A nova resposta.</returns>
    /// <exception cref="ArgumentException">Se algum dos identificadores estiver vazio.</exception>
    public static Match Decline(Guid candidateId, Guid jobId, DateTimeOffset now)
    {
        return new Match(candidateId, jobId, MatchStatus.DeclinedByCandidate, now);
    }

    /// <summary>
    /// Regista o interesse do Candidato na vaga (RF019, T2), no estado <see cref="MatchStatus.Waiting"/>, sem prazo de
    /// expiração (RF113). A quota e a visibilidade da vaga são verificadas pelo serviço.
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="now">Instante do interesse, em UTC.</param>
    /// <returns>A nova resposta.</returns>
    /// <exception cref="ArgumentException">Se algum dos identificadores estiver vazio.</exception>
    public static Match ExpressInterest(Guid candidateId, Guid jobId, DateTimeOffset now)
    {
        return new Match(candidateId, jobId, MatchStatus.Waiting, now);
    }

    /// <summary>
    /// Indica se a resposta é um interesse, ou seja, qualquer estado diferente de
    /// <see cref="MatchStatus.DeclinedByCandidate"/> (DM-21).
    /// </summary>
    /// <returns><see langword="true"/> se o Candidato manifestou interesse na vaga.</returns>
    public bool IsInterest()
    {
        return this.Status != MatchStatus.DeclinedByCandidate;
    }

    /// <summary>
    /// Regista a abertura do perfil completo pelo Recrutador (RF061, RF118, T3). Na primeira abertura preenche
    /// <see cref="ProfileOpenedAt"/>; nas seguintes não altera nada.
    /// </summary>
    /// <param name="now">Instante da abertura, em UTC.</param>
    /// <returns><see langword="true"/> na primeira abertura e <see langword="false"/> nas seguintes.</returns>
    /// <exception cref="InvalidOperationException">Se o interesse já não estiver em espera.</exception>
    public bool OpenProfile(DateTimeOffset now)
    {
        this.EnsureWaiting("Só se abre o perfil de um interesse em espera.");

        if (this.ProfileOpenedAt is not null)
        {
            return false;
        }

        this.ProfileOpenedAt = now;
        return true;
    }

    /// <summary>
    /// Aceita o Candidato (RF063, RF065, RF066, RF068, T5): <see cref="MatchStatus.Waiting"/> →
    /// <see cref="MatchStatus.Matched"/>, com o autor e a data da decisão, e abre a conversa.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da Empresa da vaga.</param>
    /// <param name="now">Instante da decisão, em UTC.</param>
    /// <exception cref="ArgumentException">Se o identificador do Recrutador estiver vazio.</exception>
    /// <exception cref="InvalidOperationException">
    /// Se o interesse já não estiver em espera, porque a decisão já foi registada ou a vaga foi recusada (RF117), ou se o
    /// perfil ainda não tiver sido aberto (T4).
    /// </exception>
    public void Accept(Guid recruiterId, DateTimeOffset now)
    {
        this.Decide(recruiterId, now);

        this.Status = MatchStatus.Matched;
        this.ConversationStatus = Enums.ConversationStatus.Open;
    }

    /// <summary>
    /// Recusa o Candidato (RF064, RF065, T6): <see cref="MatchStatus.Waiting"/> →
    /// <see cref="MatchStatus.RejectedByRecruiter"/>, com o autor e a data da decisão. Não há conversa.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da Empresa da vaga.</param>
    /// <param name="now">Instante da decisão, em UTC.</param>
    /// <exception cref="ArgumentException">Se o identificador do Recrutador estiver vazio.</exception>
    /// <exception cref="InvalidOperationException">
    /// Se o interesse já não estiver em espera (RF117) ou se o perfil ainda não tiver sido aberto (T4).
    /// </exception>
    public void Reject(Guid recruiterId, DateTimeOffset now)
    {
        this.Decide(recruiterId, now);

        this.Status = MatchStatus.RejectedByRecruiter;
    }

    /// <summary>
    /// Passa a conversa ao modo apenas de consulta (RF030, RF074, RF075), com a data, o motivo e, quando é uma das
    /// partes, o autor.
    /// </summary>
    /// <param name="closedById">
    /// Identificador da conta que encerrou, obrigatório com <see cref="Enums.ConversationCloseReason.ClosedByParty"/> e
    /// vazio nos encerramentos automáticos (bloqueio ou suspensão de conta, suspensão de Empresa).
    /// </param>
    /// <param name="reason">Motivo do encerramento.</param>
    /// <param name="now">Instante do encerramento, em UTC.</param>
    /// <exception cref="ArgumentException">
    /// Se o motivo não pertencer à enumeração, se faltar o autor no encerramento por uma das partes ou se houver autor
    /// num encerramento automático.
    /// </exception>
    /// <exception cref="InvalidOperationException">Se a conversa não estiver aberta.</exception>
    public void CloseConversation(Guid? closedById, ConversationCloseReason reason, DateTimeOffset now)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentException("O motivo de encerramento não pertence à lista (RF075).", nameof(reason));
        }

        bool closedByParty = reason == Enums.ConversationCloseReason.ClosedByParty;

        if (closedByParty != (closedById is not null && closedById != Guid.Empty))
        {
            throw new ArgumentException(
                "O autor só é indicado, e é obrigatório, quando a conversa é encerrada por uma das partes.",
                nameof(closedById));
        }

        if (this.Status != MatchStatus.Matched || this.ConversationStatus != Enums.ConversationStatus.Open)
        {
            throw new InvalidOperationException("Só se encerra uma conversa aberta.");
        }

        this.ConversationStatus = Enums.ConversationStatus.Closed;
        this.ConversationClosedAt = now;
        this.ConversationClosedById = closedById;
        this.ConversationCloseReason = reason;
    }

    /// <summary>
    /// Indica se podem ser enviadas mensagens: só com o match confirmado e a conversa aberta (RF069, RF070).
    /// </summary>
    /// <returns><see langword="true"/> se a conversa estiver aberta.</returns>
    public bool CanSendMessages()
    {
        return this.Status == MatchStatus.Matched && this.ConversationStatus == Enums.ConversationStatus.Open;
    }

    private void Decide(Guid recruiterId, DateTimeOffset now)
    {
        if (recruiterId == Guid.Empty)
        {
            throw new ArgumentException("O identificador do Recrutador é obrigatório.", nameof(recruiterId));
        }

        this.EnsureWaiting("O interesse já não está em espera: a decisão já foi registada ou a vaga foi recusada (RF117).");

        if (this.ProfileOpenedAt is null)
        {
            throw new InvalidOperationException("O perfil completo tem de ser aberto antes de registar a decisão (RF063, RF064).");
        }

        this.DecidedById = recruiterId;
        this.RecruiterActionAt = now;
    }

    private void EnsureWaiting(string message)
    {
        if (this.Status != MatchStatus.Waiting)
        {
            throw new InvalidOperationException(message);
        }
    }
}
