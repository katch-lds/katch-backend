// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="Match"/>: estados e transições T1 a T6 do diagrama de estados do interesse e do match,
/// abertura do perfil, decisão do Recrutador, conversa e encerramento (modelo de dados v02, secções 6.13 e 7.1; modelo
/// de classes do backend v02, secção 3.4).
/// </summary>
public sealed class MatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid CandidateId = Guid.NewGuid();

    private static readonly Guid JobId = Guid.NewGuid();

    private static readonly Guid RecruiterId = Guid.NewGuid();

    /// <summary>
    /// T1: a recusa da vaga fica no estado <see cref="MatchStatus.DeclinedByCandidate"/>, com a data da recusa e sem
    /// perfil aberto, decisão nem conversa (RF018).
    /// </summary>
    [Fact]
    public void DeclineCreatesADeclinedAnswerWithoutDecisionOrConversation()
    {
        var match = Match.Decline(CandidateId, JobId, Now);

        Assert.NotEqual(Guid.Empty, match.Id);
        Assert.Equal(CandidateId, match.CandidateId);
        Assert.Equal(JobId, match.JobId);
        Assert.Equal(MatchStatus.DeclinedByCandidate, match.Status);
        Assert.Equal(Now, match.CandidateActionAt);
        Assert.False(match.IsInterest());
        AssertNoDecisionAndNoConversation(match);
        Assert.Null(match.ProfileOpenedAt);
    }

    /// <summary>
    /// T2: o interesse fica em espera da decisão do Recrutador, com a data do interesse e sem prazo de expiração
    /// (RF019, RF113).
    /// </summary>
    [Fact]
    public void ExpressInterestCreatesAWaitingAnswer()
    {
        var match = Match.ExpressInterest(CandidateId, JobId, Now);

        Assert.Equal(MatchStatus.Waiting, match.Status);
        Assert.Equal(Now, match.CandidateActionAt);
        Assert.True(match.IsInterest());
        Assert.Null(match.ProfileOpenedAt);
        AssertNoDecisionAndNoConversation(match);
    }

    /// <summary>
    /// Os identificadores do Candidato e da vaga são obrigatórios nas duas fábricas.
    /// </summary>
    [Fact]
    public void EmptyIdentifiersAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Match.Decline(Guid.Empty, JobId, Now));
        Assert.Throws<ArgumentException>(() => Match.Decline(CandidateId, Guid.Empty, Now));
        Assert.Throws<ArgumentException>(() => Match.ExpressInterest(Guid.Empty, JobId, Now));
        Assert.Throws<ArgumentException>(() => Match.ExpressInterest(CandidateId, Guid.Empty, Now));
    }

    /// <summary>
    /// Cada resposta tem o seu identificador.
    /// </summary>
    [Fact]
    public void EachAnswerHasItsOwnIdentifier()
    {
        Assert.NotEqual(Match.Decline(CandidateId, JobId, Now).Id, Match.Decline(CandidateId, JobId, Now).Id);
    }

    /// <summary>
    /// T3: a primeira abertura do perfil regista a data e devolve <see langword="true"/>; as seguintes não alteram
    /// nada e devolvem <see langword="false"/> (RF061, RF118). O estado mantém-se em espera.
    /// </summary>
    [Fact]
    public void OpenProfileRegistersOnlyTheFirstOpening()
    {
        Match match = NewWaiting();

        Assert.True(match.OpenProfile(Now));
        Assert.False(match.OpenProfile(Now.AddHours(1)));

        Assert.Equal(Now, match.ProfileOpenedAt);
        Assert.Equal(MatchStatus.Waiting, match.Status);
        AssertNoDecisionAndNoConversation(match);
    }

    /// <summary>
    /// O perfil só se abre com o interesse em espera: não se abre o de uma recusa da vaga nem o de um interesse já
    /// decidido (ERR-35).
    /// </summary>
    [Fact]
    public void OpenProfileRequiresAWaitingInterest()
    {
        Assert.Throws<InvalidOperationException>(() => Match.Decline(CandidateId, JobId, Now).OpenProfile(Now));
        Assert.Throws<InvalidOperationException>(() => NewMatched().OpenProfile(Now));
        Assert.Throws<InvalidOperationException>(() => NewRejected().OpenProfile(Now));
    }

    /// <summary>
    /// T5: aceitar com o perfil aberto confirma o match, regista o autor e a data da decisão e abre a conversa (RF063,
    /// RF065, RF066, RF068).
    /// </summary>
    [Fact]
    public void AcceptConfirmsTheMatchAndOpensTheConversation()
    {
        Match match = NewWaiting();
        match.OpenProfile(Now);

        match.Accept(RecruiterId, Now.AddMinutes(5));

        Assert.Equal(MatchStatus.Matched, match.Status);
        Assert.Equal(RecruiterId, match.DecidedById);
        Assert.Equal(Now.AddMinutes(5), match.RecruiterActionAt);
        Assert.Equal(Now, match.ProfileOpenedAt);
        Assert.Equal(ConversationStatus.Open, match.ConversationStatus);
        Assert.Null(match.ConversationClosedAt);
        Assert.Null(match.ConversationClosedById);
        Assert.Null(match.ConversationCloseReason);
        Assert.True(match.IsInterest());
        Assert.True(match.CanSendMessages());
    }

    /// <summary>
    /// T6: recusar com o perfil aberto regista o autor e a data da decisão, sem conversa (RF064, RF065).
    /// </summary>
    [Fact]
    public void RejectRegistersTheDecisionWithoutAConversation()
    {
        Match match = NewWaiting();
        match.OpenProfile(Now);

        match.Reject(RecruiterId, Now.AddMinutes(5));

        Assert.Equal(MatchStatus.RejectedByRecruiter, match.Status);
        Assert.Equal(RecruiterId, match.DecidedById);
        Assert.Equal(Now.AddMinutes(5), match.RecruiterActionAt);
        Assert.Null(match.ConversationStatus);
        Assert.True(match.IsInterest());
        Assert.False(match.CanSendMessages());
    }

    /// <summary>
    /// T4: a decisão sem o perfil aberto é rejeitada e o interesse continua em espera, sem decisão (RF063, RF064; ERR-34).
    /// </summary>
    [Fact]
    public void DecisionWithoutAnOpenedProfileIsRejectedAndNothingChanges()
    {
        Match accepted = NewWaiting();
        Match rejected = NewWaiting();

        Assert.Throws<InvalidOperationException>(() => accepted.Accept(RecruiterId, Now));
        Assert.Throws<InvalidOperationException>(() => rejected.Reject(RecruiterId, Now));

        foreach (Match match in new[] { accepted, rejected })
        {
            Assert.Equal(MatchStatus.Waiting, match.Status);
            AssertNoDecisionAndNoConversation(match);
        }
    }

    /// <summary>
    /// Uma decisão registada não é alterada: aceitar ou recusar de novo, depois de aceitar ou de recusar, é rejeitado e
    /// mantém a primeira decisão (RF117; ERR-35).
    /// </summary>
    [Fact]
    public void ARegisteredDecisionIsNeverChanged()
    {
        Match matched = NewMatched();
        Assert.Throws<InvalidOperationException>(() => matched.Reject(Guid.NewGuid(), Now.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() => matched.Accept(Guid.NewGuid(), Now.AddDays(1)));
        Assert.Equal(MatchStatus.Matched, matched.Status);
        Assert.Equal(RecruiterId, matched.DecidedById);
        Assert.Equal(Now.AddMinutes(5), matched.RecruiterActionAt);
        Assert.Equal(ConversationStatus.Open, matched.ConversationStatus);

        Match rejected = NewRejected();
        Assert.Throws<InvalidOperationException>(() => rejected.Accept(Guid.NewGuid(), Now.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() => rejected.Reject(Guid.NewGuid(), Now.AddDays(1)));
        Assert.Equal(MatchStatus.RejectedByRecruiter, rejected.Status);
        Assert.Equal(RecruiterId, rejected.DecidedById);
        Assert.Null(rejected.ConversationStatus);
    }

    /// <summary>
    /// Uma vaga recusada pelo Candidato não pode ser decidida pelo Recrutador.
    /// </summary>
    [Fact]
    public void ADeclinedJobCannotBeDecided()
    {
        var match = Match.Decline(CandidateId, JobId, Now);

        Assert.Throws<InvalidOperationException>(() => match.Accept(RecruiterId, Now));
        Assert.Throws<InvalidOperationException>(() => match.Reject(RecruiterId, Now));
        Assert.Equal(MatchStatus.DeclinedByCandidate, match.Status);
        AssertNoDecisionAndNoConversation(match);
    }

    /// <summary>
    /// O Recrutador que decide tem de ser identificado.
    /// </summary>
    [Fact]
    public void DecisionRequiresTheRecruiter()
    {
        Match match = NewWaiting();
        match.OpenProfile(Now);

        Assert.Throws<ArgumentException>(() => match.Accept(Guid.Empty, Now));
        Assert.Throws<ArgumentException>(() => match.Reject(Guid.Empty, Now));
        Assert.Equal(MatchStatus.Waiting, match.Status);
        AssertNoDecisionAndNoConversation(match);
    }

    /// <summary>
    /// O interesse em espera só sai desse estado pela decisão do Recrutador: abrir o perfil não o altera (RF113).
    /// </summary>
    [Fact]
    public void AWaitingInterestOnlyLeavesTheStateByTheDecision()
    {
        Match match = NewWaiting();

        match.OpenProfile(Now);
        match.OpenProfile(Now.AddDays(30));

        Assert.Equal(MatchStatus.Waiting, match.Status);
        Assert.True(match.IsInterest());
    }

    /// <summary>
    /// O encerramento por uma das partes regista a data, o motivo e o autor e passa a conversa ao modo apenas de
    /// consulta (RF030, RF074, RF075).
    /// </summary>
    [Fact]
    public void CloseConversationByAPartyRegistersTheAuthor()
    {
        Match match = NewMatched();
        Guid authorId = Guid.NewGuid();

        match.CloseConversation(authorId, ConversationCloseReason.ClosedByParty, Now.AddDays(1));

        Assert.Equal(ConversationStatus.Closed, match.ConversationStatus);
        Assert.Equal(Now.AddDays(1), match.ConversationClosedAt);
        Assert.Equal(authorId, match.ConversationClosedById);
        Assert.Equal(ConversationCloseReason.ClosedByParty, match.ConversationCloseReason);
        Assert.Equal(MatchStatus.Matched, match.Status);
        Assert.False(match.CanSendMessages());
    }

    /// <summary>
    /// O encerramento automático (bloqueio ou suspensão de conta, suspensão de Empresa) não tem autor (RF075).
    /// </summary>
    /// <param name="reason">Motivo do encerramento automático.</param>
    [Theory]
    [InlineData(ConversationCloseReason.AccountBlockedOrSuspended)]
    [InlineData(ConversationCloseReason.CompanySuspended)]
    public void AutomaticClosingHasNoAuthor(ConversationCloseReason reason)
    {
        Match match = NewMatched();

        match.CloseConversation(null, reason, Now.AddDays(1));

        Assert.Equal(ConversationStatus.Closed, match.ConversationStatus);
        Assert.Equal(reason, match.ConversationCloseReason);
        Assert.Null(match.ConversationClosedById);
        Assert.Equal(Now.AddDays(1), match.ConversationClosedAt);
    }

    /// <summary>
    /// O autor é obrigatório no encerramento por uma das partes e não existe nos automáticos (<c>ck_match_closed_by</c>).
    /// </summary>
    [Fact]
    public void TheAuthorOfTheClosingMatchesTheReason()
    {
        Match match = NewMatched();

        Assert.Throws<ArgumentException>(() => match.CloseConversation(null, ConversationCloseReason.ClosedByParty, Now));
        Assert.Throws<ArgumentException>(() => match.CloseConversation(Guid.Empty, ConversationCloseReason.ClosedByParty, Now));
        Assert.Throws<ArgumentException>(() => match.CloseConversation(Guid.NewGuid(), ConversationCloseReason.CompanySuspended, Now));
        Assert.Throws<ArgumentException>(() => match.CloseConversation(Guid.NewGuid(), ConversationCloseReason.AccountBlockedOrSuspended, Now));
        Assert.Throws<ArgumentException>(() => match.CloseConversation(null, (ConversationCloseReason)99, Now));

        Assert.Equal(ConversationStatus.Open, match.ConversationStatus);
        Assert.Null(match.ConversationClosedAt);
        Assert.Null(match.ConversationCloseReason);
    }

    /// <summary>
    /// Só se encerra uma conversa aberta: não se encerra duas vezes (o primeiro motivo mantém-se, RF075) nem a conversa
    /// de um interesse sem match.
    /// </summary>
    [Fact]
    public void OnlyAnOpenConversationIsClosed()
    {
        Match closed = NewMatched();
        Guid authorId = Guid.NewGuid();
        closed.CloseConversation(authorId, ConversationCloseReason.ClosedByParty, Now.AddDays(1));

        Assert.Throws<InvalidOperationException>(() => closed.CloseConversation(null, ConversationCloseReason.CompanySuspended, Now.AddDays(2)));
        Assert.Equal(ConversationCloseReason.ClosedByParty, closed.ConversationCloseReason);
        Assert.Equal(authorId, closed.ConversationClosedById);
        Assert.Equal(Now.AddDays(1), closed.ConversationClosedAt);

        Assert.Throws<InvalidOperationException>(() => NewWaiting().CloseConversation(null, ConversationCloseReason.CompanySuspended, Now));
        Assert.Throws<InvalidOperationException>(() => NewRejected().CloseConversation(null, ConversationCloseReason.CompanySuspended, Now));
        Assert.Throws<InvalidOperationException>(() => Match.Decline(CandidateId, JobId, Now).CloseConversation(null, ConversationCloseReason.CompanySuspended, Now));
    }

    /// <summary>
    /// Só se enviam mensagens com o match confirmado e a conversa aberta (RF069, RF070).
    /// </summary>
    [Fact]
    public void MessagesCanOnlyBeSentInAnOpenConversationOfAConfirmedMatch()
    {
        Assert.False(Match.Decline(CandidateId, JobId, Now).CanSendMessages());
        Assert.False(NewWaiting().CanSendMessages());
        Assert.False(NewRejected().CanSendMessages());
        Assert.True(NewMatched().CanSendMessages());

        Match closed = NewMatched();
        closed.CloseConversation(null, ConversationCloseReason.CompanySuspended, Now);
        Assert.False(closed.CanSendMessages());
    }

    private static Match NewWaiting()
    {
        return Match.ExpressInterest(CandidateId, JobId, Now);
    }

    private static Match NewMatched()
    {
        Match match = NewWaiting();
        match.OpenProfile(Now);
        match.Accept(RecruiterId, Now.AddMinutes(5));
        return match;
    }

    private static Match NewRejected()
    {
        Match match = NewWaiting();
        match.OpenProfile(Now);
        match.Reject(RecruiterId, Now.AddMinutes(5));
        return match;
    }

    private static void AssertNoDecisionAndNoConversation(Match match)
    {
        Assert.Null(match.RecruiterActionAt);
        Assert.Null(match.DecidedById);
        Assert.Null(match.ConversationStatus);
        Assert.Null(match.ConversationClosedAt);
        Assert.Null(match.ConversationClosedById);
        Assert.Null(match.ConversationCloseReason);
    }
}
