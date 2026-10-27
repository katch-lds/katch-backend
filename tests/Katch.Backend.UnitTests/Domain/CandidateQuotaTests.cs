// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Reflection;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da regra da quota de interesses e do bloqueio de 24 horas (RF019 a RF022, P11) em
/// <see cref="Candidate.ConsumeInterest"/>, <see cref="Candidate.RestoreQuotaIfDue"/> e
/// <see cref="Candidate.HasInterestAvailable"/>, com o instante fixado em cada teste (parâmetro <c>now</c>).
/// </summary>
public sealed class CandidateQuotaTests
{
    private static readonly DateTimeOffset LastInterest = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Block = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets the instants before the end of the block period, counted from the interest that exhausted the quota.
    /// </summary>
    public static TheoryData<TimeSpan> BeforeTheEnd => new()
    {
        TimeSpan.Zero,
        TimeSpan.FromTicks(1),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(23),
        TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(59),
        Block - TimeSpan.FromTicks(1),
    };

    /// <summary>
    /// Gets the instants at the end of the block period and after it, counted from the interest that exhausted the quota.
    /// </summary>
    public static TheoryData<TimeSpan> FromTheEndOn => new()
    {
        Block,
        Block + TimeSpan.FromTicks(1),
        Block + TimeSpan.FromSeconds(1),
        TimeSpan.FromHours(25),
        TimeSpan.FromHours(48),
        TimeSpan.FromDays(7),
        TimeSpan.FromDays(365),
    };

    /// <summary>
    /// Gets the combinations of interests consumed (fewer than 10) and time elapsed.
    /// </summary>
    public static TheoryData<int, TimeSpan> UnexhaustedQuotas
    {
        get
        {
            var data = new TheoryData<int, TimeSpan>();
            TimeSpan[] elapsed = [TimeSpan.FromMinutes(1), Block, TimeSpan.FromHours(25), TimeSpan.FromDays(30)];

            for (int consumed = 0; consumed < Candidate.MaxInterests; consumed++)
            {
                foreach (TimeSpan time in elapsed)
                {
                    data.Add(consumed, time);
                }
            }

            return data;
        }
    }

    /// <summary>
    /// Gets the exhaustion instant, the instant of the attempt (both ISO 8601) and whether an interest is accepted then;
    /// the block is counted from the exact instant of the last interest, never from midnight.
    /// </summary>
    public static TheoryData<string, string, bool> AroundMidnight => new()
    {
        { "2026-10-16T23:59:59Z", "2026-10-17T00:00:00Z", false },
        { "2026-10-16T23:59:59Z", "2026-10-17T12:00:00Z", false },
        { "2026-10-16T23:59:59Z", "2026-10-17T23:59:58Z", false },
        { "2026-10-16T23:59:59Z", "2026-10-17T23:59:59Z", true },
        { "2026-10-16T00:00:00Z", "2026-10-16T23:59:59Z", false },
        { "2026-10-16T00:00:00Z", "2026-10-17T00:00:00Z", true },
        { "2026-10-16T23:30:00Z", "2026-10-17T00:30:00Z", false },
        { "2026-10-16T23:30:00Z", "2026-10-17T23:29:59Z", false },
        { "2026-10-16T23:30:00Z", "2026-10-17T23:30:00Z", true },
        { "2026-12-31T23:00:00Z", "2027-01-01T22:59:59Z", false },
        { "2026-12-31T23:00:00Z", "2027-01-01T23:00:00Z", true },
        { "2028-02-28T12:00:00Z", "2028-02-29T11:59:59Z", false },
        { "2028-02-28T12:00:00Z", "2028-02-29T12:00:00Z", true },
    };

    /// <summary>
    /// A quota começa em 10 e cada interesse reduz-a em 1, sem bloqueio enquanto restarem interesses; o nono deixa 1.
    /// </summary>
    [Fact]
    public void QuotaStartsAtTenAndEachInterestConsumesExactlyOne()
    {
        Candidate candidate = CreateCandidate();
        Assert.Equal(10, candidate.SwipeRightsRemaining);

        for (int consumed = 1; consumed < Candidate.MaxInterests; consumed++)
        {
            candidate.ConsumeInterest(LastInterest.AddMinutes(consumed));

            Assert.Equal(10 - consumed, candidate.SwipeRightsRemaining);
            Assert.Null(candidate.NextSwipeAvailableAt);
            Assert.True(candidate.HasInterestAvailable(LastInterest.AddMinutes(consumed)));
        }

        Assert.Equal(1, candidate.SwipeRightsRemaining);
    }

    /// <summary>
    /// O décimo interesse esgota a quota e fixa o fim do bloqueio 24 horas depois desse mesmo instante.
    /// </summary>
    [Fact]
    public void TenthInterestExhaustsTheQuotaAndSetsTheEndOfTheBlockTwentyFourHoursLater()
    {
        Candidate candidate = CreateCandidate();

        ExhaustAt(candidate, LastInterest);

        Assert.Equal(0, candidate.SwipeRightsRemaining);
        Assert.Equal(LastInterest + Block, candidate.NextSwipeAvailableAt);
        Assert.False(candidate.HasInterestAvailable(LastInterest));
    }

    /// <summary>
    /// O bloqueio conta do décimo interesse e não do primeiro: interesses espalhados por três dias não encurtam o bloqueio.
    /// </summary>
    [Fact]
    public void BlockIsCountedFromTheTenthInterestAndNotFromTheFirstOne()
    {
        Candidate candidate = CreateCandidate();
        var first = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        var tenth = new DateTimeOffset(2026, 10, 16, 23, 59, 59, TimeSpan.Zero);

        for (int index = 0; index < Candidate.MaxInterests - 1; index++)
        {
            candidate.ConsumeInterest(first.AddHours(index * 5));
        }

        Assert.Null(candidate.NextSwipeAvailableAt);

        candidate.ConsumeInterest(tenth);

        Assert.Equal(tenth + Block, candidate.NextSwipeAvailableAt);
        Assert.False(candidate.HasInterestAvailable(first + Block));
        Assert.False(candidate.HasInterestAvailable(tenth + Block - TimeSpan.FromSeconds(1)));
        Assert.True(candidate.HasInterestAvailable(tenth + Block));
    }

    /// <summary>
    /// Antes do fim do bloqueio o interesse é rejeitado (ERR-31) e a quota e o fim do bloqueio ficam como estavam.
    /// </summary>
    /// <param name="sinceLastInterest">Tempo decorrido desde o interesse que esgotou a quota.</param>
    [Theory]
    [MemberData(nameof(BeforeTheEnd))]
    public void InterestIsRejectedBeforeTheEndOfTheBlock(TimeSpan sinceLastInterest)
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, LastInterest);
        DateTimeOffset now = LastInterest + sinceLastInterest;

        Assert.False(candidate.HasInterestAvailable(now));
        Assert.Throws<QuotaExhaustedException>(() => candidate.ConsumeInterest(now));
        Assert.False(candidate.RestoreQuotaIfDue(now));

        Assert.Equal(0, candidate.SwipeRightsRemaining);
        Assert.Equal(LastInterest + Block, candidate.NextSwipeAvailableAt);
    }

    /// <summary>
    /// No limite exato das 24 horas o interesse é aceite, com a quota reposta na totalidade antes de consumir.
    /// </summary>
    [Fact]
    public void InterestAtTheExactEndOfTheBlockIsAcceptedWithTheQuotaRestoredFirst()
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, LastInterest);
        DateTimeOffset end = LastInterest + Block;

        Assert.True(candidate.HasInterestAvailable(end));
        candidate.ConsumeInterest(end);

        Assert.Equal(9, candidate.SwipeRightsRemaining);
        Assert.Null(candidate.NextSwipeAvailableAt);
    }

    /// <summary>
    /// Um tick antes do fim ainda está bloqueado e no próprio fim já não está.
    /// </summary>
    [Fact]
    public void OneTickBeforeTheEndIsBlockedAndTheEndItselfIsNot()
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, LastInterest);
        DateTimeOffset end = LastInterest + Block;

        Assert.False(candidate.HasInterestAvailable(end.AddTicks(-1)));
        Assert.False(candidate.RestoreQuotaIfDue(end.AddTicks(-1)));
        Assert.True(candidate.HasInterestAvailable(end));
        Assert.True(candidate.RestoreQuotaIfDue(end));
    }

    /// <summary>
    /// Do fim do bloqueio em diante o interesse é aceite, seja qual for o tempo decorrido, e a quota volta a 10 antes de consumir.
    /// </summary>
    /// <param name="sinceLastInterest">Tempo decorrido desde o interesse que esgotou a quota.</param>
    [Theory]
    [MemberData(nameof(FromTheEndOn))]
    public void InterestFromTheEndOfTheBlockOnIsAccepted(TimeSpan sinceLastInterest)
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, LastInterest);
        DateTimeOffset now = LastInterest + sinceLastInterest;

        Assert.True(candidate.HasInterestAvailable(now));
        candidate.ConsumeInterest(now);

        Assert.Equal(9, candidate.SwipeRightsRemaining);
        Assert.Null(candidate.NextSwipeAvailableAt);
    }

    /// <summary>
    /// A reposição é sempre integral: do fim do bloqueio em diante a quota é 10, nunca mais, e repor outra vez não faz nada.
    /// </summary>
    /// <param name="sinceLastInterest">Tempo decorrido desde o interesse que esgotou a quota.</param>
    [Theory]
    [MemberData(nameof(FromTheEndOn))]
    public void RestoreIsAlwaysFullAndHappensOnlyOnce(TimeSpan sinceLastInterest)
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, LastInterest);
        DateTimeOffset now = LastInterest + sinceLastInterest;

        Assert.True(candidate.RestoreQuotaIfDue(now));
        Assert.Equal(Candidate.MaxInterests, candidate.SwipeRightsRemaining);
        Assert.Null(candidate.NextSwipeAvailableAt);

        Assert.False(candidate.RestoreQuotaIfDue(now));
        Assert.False(candidate.RestoreQuotaIfDue(now + Block));
        Assert.Equal(Candidate.MaxInterests, candidate.SwipeRightsRemaining);
    }

    /// <summary>
    /// Com menos de 10 interesses consumidos a quota não aumenta com o tempo, por muito que passe (RF021).
    /// </summary>
    /// <param name="consumed">Interesses consumidos, de 0 a 9.</param>
    /// <param name="elapsed">Tempo decorrido desde o último interesse.</param>
    [Theory]
    [MemberData(nameof(UnexhaustedQuotas))]
    public void QuotaDoesNotGrowWithTimeWhenFewerThanTenInterestsWereConsumed(int consumed, TimeSpan elapsed)
    {
        Candidate candidate = CreateCandidate();
        for (int index = 0; index < consumed; index++)
        {
            candidate.ConsumeInterest(LastInterest);
        }

        DateTimeOffset now = LastInterest + elapsed;

        Assert.False(candidate.RestoreQuotaIfDue(now));
        Assert.True(candidate.HasInterestAvailable(now));
        Assert.Equal(10 - consumed, candidate.SwipeRightsRemaining);
        Assert.Null(candidate.NextSwipeAvailableAt);
    }

    /// <summary>
    /// O bloqueio dura 24 horas desde o instante exato do último interesse e não acaba à meia-noite, nem num dia de
    /// calendário, nem na passagem de ano ou de dia bissexto.
    /// </summary>
    /// <param name="exhaustedAt">Instante do interesse que esgota a quota (ISO 8601).</param>
    /// <param name="attemptAt">Instante da tentativa (ISO 8601).</param>
    /// <param name="accepted">Se o interesse é aceite nesse instante.</param>
    [Theory]
    [MemberData(nameof(AroundMidnight))]
    public void BlockEndsTwentyFourHoursAfterTheLastInterestAndNotAtMidnight(string exhaustedAt, string attemptAt, bool accepted)
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, Parse(exhaustedAt));
        DateTimeOffset now = Parse(attemptAt);

        Assert.Equal(accepted, candidate.HasInterestAvailable(now));
        if (accepted)
        {
            candidate.ConsumeInterest(now);
            Assert.Equal(9, candidate.SwipeRightsRemaining);
        }
        else
        {
            Assert.Throws<QuotaExhaustedException>(() => candidate.ConsumeInterest(now));
            Assert.Equal(0, candidate.SwipeRightsRemaining);
        }
    }

    /// <summary>
    /// As 24 horas são absolutas: na noite da mudança de hora (25 de outubro de 2026, 01:00 UTC) o bloqueio acaba às
    /// 12:00 UTC e não às 13:00, que seria «a mesma hora do dia seguinte» no relógio de parede.
    /// </summary>
    [Fact]
    public void BlockIsTwentyFourElapsedHoursAcrossTheClockChange()
    {
        Candidate candidate = CreateCandidate();
        var summerTime = new DateTimeOffset(2026, 10, 24, 13, 0, 0, TimeSpan.FromHours(1));
        ExhaustAt(candidate, summerTime);

        var beforeTheEnd = new DateTimeOffset(2026, 10, 25, 11, 59, 59, TimeSpan.Zero);
        var atTheEnd = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        Assert.False(candidate.HasInterestAvailable(beforeTheEnd));
        Assert.True(candidate.HasInterestAvailable(atTheEnd));
    }

    /// <summary>
    /// Os instantes são comparados como instantes absolutos, seja qual for o desfasamento horário com que são indicados.
    /// </summary>
    [Fact]
    public void InstantsAreComparedAsAbsoluteInstantsWhateverTheirOffset()
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, new DateTimeOffset(2026, 10, 16, 23, 30, 0, TimeSpan.FromHours(1)));

        Assert.False(candidate.HasInterestAvailable(new DateTimeOffset(2026, 10, 17, 23, 29, 59, TimeSpan.FromHours(1))));
        Assert.True(candidate.HasInterestAvailable(new DateTimeOffset(2026, 10, 17, 22, 30, 0, TimeSpan.Zero)));
        Assert.True(candidate.HasInterestAvailable(new DateTimeOffset(2026, 10, 17, 23, 30, 0, TimeSpan.FromHours(1))));
    }

    /// <summary>
    /// A rejeição traz o fim do bloqueio e o tempo em falta contado do instante da tentativa.
    /// </summary>
    /// <param name="sinceLastInterest">Tempo decorrido desde o interesse que esgotou a quota.</param>
    [Theory]
    [MemberData(nameof(BeforeTheEnd))]
    public void RejectionCarriesTheEndOfTheBlockAndTheTimeLeft(TimeSpan sinceLastInterest)
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, LastInterest);
        DateTimeOffset now = LastInterest + sinceLastInterest;

        QuotaExhaustedException rejection = Assert.Throws<QuotaExhaustedException>(() => candidate.ConsumeInterest(now));

        Assert.Equal(LastInterest + Block, rejection.BlockedUntil);
        Assert.Equal(Block - sinceLastInterest, rejection.Remaining);
        Assert.True(rejection.Remaining > TimeSpan.Zero);
    }

    /// <summary>
    /// Insistir durante o bloqueio, um pedido por minuto, não o prolonga nem altera a quota.
    /// </summary>
    [Fact]
    public void RepeatedRejectionsNeverExtendTheBlockNorChangeTheQuota()
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, LastInterest);

        for (int minute = 1; minute <= 120; minute++)
        {
            DateTimeOffset now = LastInterest.AddMinutes(minute);
            Assert.Throws<QuotaExhaustedException>(() => candidate.ConsumeInterest(now));
        }

        Assert.Equal(0, candidate.SwipeRightsRemaining);
        Assert.Equal(LastInterest + Block, candidate.NextSwipeAvailableAt);
    }

    /// <summary>
    /// Depois da reposição, o bloqueio seguinte conta do novo décimo interesse.
    /// </summary>
    [Fact]
    public void NextBlockIsCountedFromTheNewTenthInterest()
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, LastInterest);
        DateTimeOffset secondRound = LastInterest.AddHours(30);

        ExhaustAt(candidate, secondRound);

        Assert.Equal(0, candidate.SwipeRightsRemaining);
        Assert.Equal(secondRound + Block, candidate.NextSwipeAvailableAt);
        Assert.False(candidate.HasInterestAvailable(secondRound + Block - TimeSpan.FromMinutes(1)));
        Assert.True(candidate.HasInterestAvailable(secondRound + Block));
    }

    /// <summary>
    /// Consultar se há interesse disponível não altera o estado: só a reposição e o interesse o alteram.
    /// </summary>
    [Fact]
    public void QueryingTheQuotaNeverChangesIt()
    {
        Candidate candidate = CreateCandidate();
        ExhaustAt(candidate, LastInterest);
        DateTimeOffset end = LastInterest + Block;

        Assert.True(candidate.HasInterestAvailable(end));
        Assert.True(candidate.HasInterestAvailable(end.AddDays(3)));

        Assert.Equal(0, candidate.SwipeRightsRemaining);
        Assert.Equal(end, candidate.NextSwipeAvailableAt);
    }

    /// <summary>
    /// Uma quota a 0 sem o fim do bloqueio (que o CHECK da tabela admite) é um erro de dados e não «quota esgotada»: não
    /// pode ser convertida no erro ERR-31, e continua sem permitir interesses.
    /// </summary>
    [Fact]
    public void ZeroQuotaWithoutTheEndOfTheBlockIsInvalidDataAndNotAnExhaustedQuota()
    {
        Candidate candidate = CreateCandidate();
        typeof(Candidate)
            .GetProperty(nameof(Candidate.SwipeRightsRemaining), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(candidate, (short)0);

        Assert.False(candidate.HasInterestAvailable(LastInterest.AddDays(30)));
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => candidate.ConsumeInterest(LastInterest));
        Assert.IsNotType<QuotaExhaustedException>(error);
        Assert.Equal(0, candidate.SwipeRightsRemaining);
    }

    /// <summary>
    /// A rejeição é uma <see cref="InvalidOperationException"/>, como a operação documenta, com mensagem sem dados pessoais.
    /// </summary>
    [Fact]
    public void RejectionIsAnInvalidOperationWithoutPersonalData()
    {
        var rejection = new QuotaExhaustedException(LastInterest + Block, TimeSpan.FromHours(1));

        Assert.IsAssignableFrom<InvalidOperationException>(rejection);
        Assert.Equal("A quota de interesses está esgotada até ao fim do período de bloqueio.", rejection.Message);
    }

    private static DateTimeOffset Parse(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    private static Candidate CreateCandidate()
    {
        var user = new AppUser("candidato@katch.pt", "hash", UserType.Candidate, LastInterest, LastInterest);
        return new Candidate(user, "Ana Silva", "912345678", Guid.NewGuid());
    }

    // Consome interesses até esgotar a quota, um por minuto, de modo que o décimo (o que esgota) é no instante indicado.
    // Se o período anterior já terminou, a quota é reposta pelo primeiro e o ciclo recomeça.
    private static void ExhaustAt(Candidate candidate, DateTimeOffset lastInterest)
    {
        for (int index = 0; index < Candidate.MaxInterests; index++)
        {
            candidate.ConsumeInterest(lastInterest.AddMinutes(index - (Candidate.MaxInterests - 1)));
        }
    }
}
