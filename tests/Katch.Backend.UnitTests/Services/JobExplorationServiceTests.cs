// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes de <see cref="JobExplorationService"/>: recusa, interesse e consulta da quota (RF014, RF018 a RF022, RF112,
/// RF113, P11), com os repositórios simulados e o instante do relógio do servidor fixado em cada teste. A concorrência
/// (<c>xmin</c>) e o índice <c>uq_match_candidate_job</c> só se exercitam aqui, porque o SQLite dos testes de integração
/// não os tem.
/// </summary>
public sealed class JobExplorationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A recusa regista a resposta <c>DECLINED_BY_CANDIDATE</c> com a data e não toca na quota.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DeclineRecordsTheAnswerAndDoesNotTouchTheQuota()
    {
        Fixture fixture = new();
        Guid jobId = fixture.AddJob();

        await fixture.Service.DeclineAsync(fixture.CandidateId, jobId, TestContext.Current.CancellationToken);

        Match match = Assert.Single(fixture.Store.Committed);
        Assert.Equal(MatchStatus.DeclinedByCandidate, match.Status);
        Assert.Equal(fixture.CandidateId, match.CandidateId);
        Assert.Equal(jobId, match.JobId);
        Assert.Equal(Now, match.CandidateActionAt);
        Assert.Equal(1, fixture.Store.Saves);
        Assert.Equal(0, fixture.Candidates.Loads);
    }

    /// <summary>
    /// Quinze recusas seguidas não limitam as recusas nem mexem na quota.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task FifteenDeclinesInARowLeaveTheQuotaUntouched()
    {
        Fixture fixture = new();

        for (int index = 0; index < 15; index++)
        {
            await fixture.Service.DeclineAsync(fixture.CandidateId, fixture.AddJob(), TestContext.Current.CancellationToken);
        }

        QuotaDto quota = await fixture.Service.GetQuotaAsync(fixture.CandidateId, TestContext.Current.CancellationToken);
        Assert.Equal(15, fixture.Store.Committed.Count);
        Assert.All(fixture.Store.Committed, match => Assert.Equal(MatchStatus.DeclinedByCandidate, match.Status));
        Assert.Equal(new QuotaDto(Candidate.MaxInterests, Candidate.MaxInterests, null), quota);
    }

    /// <summary>
    /// O interesse consome um da quota e grava o <c>match</c> em espera, numa só gravação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InterestConsumesOneAndRecordsAWaitingMatchInASingleSave()
    {
        Fixture fixture = new();
        Guid jobId = fixture.AddJob();

        QuotaDto quota = await fixture.Service.ExpressInterestAsync(fixture.CandidateId, jobId, TestContext.Current.CancellationToken);

        Assert.Equal(new QuotaDto(9, 10, null), quota);
        Match match = Assert.Single(fixture.Store.Committed);
        Assert.Equal(MatchStatus.Waiting, match.Status);
        Assert.Equal(Now, match.CandidateActionAt);
        Assert.Equal(1, fixture.Store.Saves);
        Assert.Equal(9, Assert.IsType<Candidate>(fixture.Candidates.Current).SwipeRightsRemaining);
    }

    /// <summary>
    /// O décimo interesse esgota a quota e devolve o fim do bloqueio, 24 horas depois.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TenthInterestSetsBlockedUntil()
    {
        Fixture fixture = new(() => CandidateWith(remaining: 1));

        QuotaDto quota = await fixture.Service.ExpressInterestAsync(
            fixture.CandidateId,
            fixture.AddJob(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new QuotaDto(0, 10, Now.AddHours(24)), quota);
    }

    /// <summary>
    /// Com a quota esgotada o interesse lança <see cref="QuotaExhaustedException"/> e nada é gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InterestWithExhaustedQuotaThrowsAndSavesNothing()
    {
        Fixture fixture = new(() => ExhaustedAt(Now.AddHours(-1)));

        QuotaExhaustedException exception = await Assert.ThrowsAsync<QuotaExhaustedException>(
            () => fixture.Service.ExpressInterestAsync(fixture.CandidateId, fixture.AddJob(), TestContext.Current.CancellationToken));

        Assert.Equal(Now.AddHours(23), exception.BlockedUntil);
        Assert.Equal(TimeSpan.FromHours(23), exception.Remaining);
        Assert.Equal(0, fixture.Store.Saves);
        Assert.Empty(fixture.Store.Pending);
        Assert.Empty(fixture.Store.Committed);
        Assert.Equal(0, Assert.IsType<Candidate>(fixture.Candidates.Current).SwipeRightsRemaining);
    }

    /// <summary>
    /// Um tick antes do fim do bloqueio a quota continua esgotada; no limite exato e depois dele é reposta antes de
    /// consumir.
    /// </summary>
    /// <param name="ticksSinceLastInterest">Tempo desde o último interesse que esgotou a quota, em ticks.</param>
    /// <param name="restored">Se a quota deve ser reposta antes de consumir.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData((24 * TimeSpan.TicksPerHour) - 1, false)]
    [InlineData(24 * TimeSpan.TicksPerHour, true)]
    [InlineData(25 * TimeSpan.TicksPerHour, true)]
    [InlineData(30 * 24 * TimeSpan.TicksPerHour, true)]
    public async Task EndOfTheBlockRestoresTheQuotaBeforeConsuming(long ticksSinceLastInterest, bool restored)
    {
        Fixture fixture = new(() => ExhaustedAt(Now - TimeSpan.FromTicks(ticksSinceLastInterest)));
        Guid jobId = fixture.AddJob();

        if (!restored)
        {
            await Assert.ThrowsAsync<QuotaExhaustedException>(
                () => fixture.Service.ExpressInterestAsync(fixture.CandidateId, jobId, TestContext.Current.CancellationToken));
            Assert.Empty(fixture.Store.Committed);
            return;
        }

        QuotaDto quota = await fixture.Service.ExpressInterestAsync(fixture.CandidateId, jobId, TestContext.Current.CancellationToken);

        Assert.Equal(new QuotaDto(9, 10, null), quota);
        Assert.Single(fixture.Store.Committed);
    }

    /// <summary>
    /// Vaga inexistente ou eliminada (o repositório não a devolve) é ERR-20, na recusa e no interesse.
    /// </summary>
    /// <param name="interest"><see langword="true"/> para o interesse e <see langword="false"/> para a recusa.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingJobIsRejectedAsNotFound(bool interest)
    {
        Fixture fixture = new();

        await AssertRejectedAsync(fixture, interest, Guid.NewGuid(), JobAnswerRejection.NotFound);
    }

    /// <summary>
    /// Uma vaga que nunca foi publicada é ERR-20, na recusa e no interesse.
    /// </summary>
    /// <param name="interest"><see langword="true"/> para o interesse e <see langword="false"/> para a recusa.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NeverPublishedJobIsRejectedAsNotFound(bool interest)
    {
        Fixture fixture = new();
        Guid jobId = fixture.AddJob(publish: false);

        await AssertRejectedAsync(fixture, interest, jobId, JobAnswerRejection.NotFound);
    }

    /// <summary>
    /// Uma resposta anterior do Candidato, recusa ou interesse, é ERR-32, na recusa e no interesse.
    /// </summary>
    /// <param name="interest"><see langword="true"/> para o interesse e <see langword="false"/> para a recusa.</param>
    /// <param name="previousInterest"><see langword="true"/> se a resposta anterior foi um interesse.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExistingAnswerIsRejectedAsAlreadyAnswered(bool interest, bool previousInterest)
    {
        Fixture fixture = new();
        Guid jobId = fixture.AddJob();
        fixture.Store.Committed.Add(previousInterest
            ? Match.ExpressInterest(fixture.CandidateId, jobId, Now.AddDays(-1))
            : Match.Decline(fixture.CandidateId, jobId, Now.AddDays(-1)));

        await AssertRejectedAsync(fixture, interest, jobId, JobAnswerRejection.AlreadyAnswered);
        Assert.Single(fixture.Store.Committed);
    }

    /// <summary>
    /// A violação do índice único <c>uq_match_candidate_job</c> (duas respostas simultâneas) é ERR-32 e nada fica gravado.
    /// </summary>
    /// <param name="interest"><see langword="true"/> para o interesse e <see langword="false"/> para a recusa.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UniqueIndexViolationIsRejectedAsAlreadyAnswered(bool interest)
    {
        Fixture fixture = new();
        Guid jobId = fixture.AddJob();
        fixture.Store.FailNextSaves.Enqueue(new UniqueConstraintViolationException("uq_match_candidate_job", new InvalidOperationException()));

        await AssertRejectedAsync(fixture, interest, jobId, JobAnswerRejection.AlreadyAnswered);
        Assert.Empty(fixture.Store.Committed);
    }

    /// <summary>
    /// Uma violação de outro índice único não é convertida em ERR-32.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OtherUniqueIndexViolationPropagates()
    {
        Fixture fixture = new();
        fixture.Store.FailNextSaves.Enqueue(new UniqueConstraintViolationException("uq_outro", new InvalidOperationException()));

        await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => fixture.Service.DeclineAsync(fixture.CandidateId, fixture.AddJob(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Uma vaga suspensa, encerrada, expirada ou de uma Empresa que não está aprovada é ERR-33, na recusa e no interesse.
    /// </summary>
    /// <param name="interest"><see langword="true"/> para o interesse e <see langword="false"/> para a recusa.</param>
    /// <param name="scenario">Motivo da indisponibilidade.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(false, "suspended")]
    [InlineData(false, "closed")]
    [InlineData(false, "expired")]
    [InlineData(false, "pending")]
    [InlineData(false, "rejected")]
    [InlineData(false, "company-suspended")]
    [InlineData(true, "suspended")]
    [InlineData(true, "closed")]
    [InlineData(true, "expired")]
    [InlineData(true, "pending")]
    [InlineData(true, "rejected")]
    [InlineData(true, "company-suspended")]
    public async Task JobThatIsNoLongerAvailableIsRejectedAsUnavailable(bool interest, string scenario)
    {
        Fixture fixture = new();
        Guid jobId = scenario switch
        {
            "suspended" => fixture.AddJob(job => job.Suspend(Now.AddDays(-1))),
            "closed" => fixture.AddJob(job => job.Close()),
            "expired" => fixture.AddJob(expiresAt: Now),
            "pending" => fixture.AddJob(company: CompanyStatus.Pending),
            "rejected" => fixture.AddJob(company: CompanyStatus.Rejected),
            _ => fixture.AddJob(company: CompanyStatus.Suspended),
        };

        await AssertRejectedAsync(fixture, interest, jobId, JobAnswerRejection.Unavailable);
    }

    /// <summary>
    /// A ordem das verificações é a da especificação: a vaga inexistente vem antes da resposta existente, e esta antes
    /// da indisponibilidade; a indisponibilidade vem antes da quota.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ChecksRunInTheDocumentedOrder()
    {
        Fixture fixture = new(() => ExhaustedAt(Now.AddHours(-1)));
        Guid answered = fixture.AddJob(job => job.Suspend(Now.AddDays(-1)));
        fixture.Store.Committed.Add(Match.Decline(fixture.CandidateId, answered, Now.AddDays(-1)));
        Guid unavailable = fixture.AddJob(job => job.Suspend(Now.AddDays(-1)));

        await AssertRejectedAsync(fixture, interest: true, answered, JobAnswerRejection.AlreadyAnswered);
        await AssertRejectedAsync(fixture, interest: true, unavailable, JobAnswerRejection.Unavailable);
        Assert.Equal(0, fixture.Candidates.Loads);
    }

    /// <summary>
    /// Perante conflitos de concorrência o interesse descarta o contexto e repete tudo desde a leitura, até acabar bem,
    /// sem gravar duas vezes a mesma resposta.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InterestRetriesAfterConcurrencyConflictsAndSucceeds()
    {
        Fixture fixture = new();
        fixture.Store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        fixture.Store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));

        QuotaDto quota = await fixture.Service.ExpressInterestAsync(
            fixture.CandidateId,
            fixture.AddJob(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new QuotaDto(9, 10, null), quota);
        Assert.Single(fixture.Store.Committed);
        Assert.Equal(2, fixture.Store.Discards);
        Assert.Equal(3, fixture.Candidates.Loads);
        Assert.Equal(1, fixture.Store.Saves);
        Assert.Equal([1, 2], fixture.Delay.Attempts);
    }

    /// <summary>
    /// A espera só acontece entre tentativas: sem conflito não há espera, e com conflitos a primeira espera vem depois da
    /// primeira leitura.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WaitsBetweenAttemptsAndNeverBeforeTheFirst()
    {
        Fixture clean = new();
        await clean.Service.ExpressInterestAsync(clean.CandidateId, clean.AddJob(), TestContext.Current.CancellationToken);
        Assert.Empty(clean.Delay.Attempts);

        Fixture conflicting = new();
        conflicting.Store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        conflicting.Store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        await conflicting.Service.ExpressInterestAsync(
            conflicting.CandidateId,
            conflicting.AddJob(),
            TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], conflicting.Delay.LoadsAtEachWait);
    }

    /// <summary>
    /// Mesmo com 30 conflitos seguidos o interesse acaba bem, enquanto o tempo total não passar do limite.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InterestSucceedsAfterThirtyConsecutiveConflicts()
    {
        Fixture fixture = new();
        for (int index = 0; index < 30; index++)
        {
            fixture.Store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        }

        QuotaDto quota = await fixture.Service.ExpressInterestAsync(
            fixture.CandidateId,
            fixture.AddJob(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new QuotaDto(9, 10, null), quota);
        Assert.Single(fixture.Store.Committed);
        Assert.Equal(30, fixture.Store.Discards);
        Assert.Equal(31, fixture.Candidates.Loads);
        Assert.Equal(1, fixture.Store.Saves);
        Assert.Equal(Enumerable.Range(1, 30), fixture.Delay.Attempts);
    }

    /// <summary>
    /// Depois de vários conflitos, a leitura seguinte já vê a quota esgotada: o interesse recebe ERR-31, nunca ERR-41, e
    /// nenhuma linha de <c>match</c> é gravada.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConflictsFollowedByAnExhaustedQuotaEndInErr31WithoutSaving()
    {
        Fixture fixture = new(() => CandidateWith(remaining: 1));
        for (int index = 0; index < 3; index++)
        {
            fixture.Store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        }

        fixture.Store.OnDiscard = () =>
        {
            if (fixture.Store.Discards == 3)
            {
                fixture.Candidates.SetSource(() => ExhaustedAt(Now.AddMinutes(-5)));
            }
        };

        QuotaExhaustedException exception = await Assert.ThrowsAsync<QuotaExhaustedException>(
            () => fixture.Service.ExpressInterestAsync(fixture.CandidateId, fixture.AddJob(), TestContext.Current.CancellationToken));

        Assert.Equal(Now.AddMinutes(-5).AddHours(24), exception.BlockedUntil);
        Assert.Equal(0, fixture.Store.Saves);
        Assert.Empty(fixture.Store.Committed);
        Assert.Empty(fixture.Store.Pending);
    }

    /// <summary>
    /// Quando o limite de tempo acaba, o interesse deixa o <see cref="ConcurrencyConflictException"/> propagar (ERR-41, o
    /// último recurso) e nada fica gravado. O tempo é o de um fornecedor falso: o teste não espera.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task InterestPropagatesTheConcurrencyConflictWhenTheTimeLimitEnds()
    {
        Fixture fixture = new();
        fixture.Store.FailAlways = new ConcurrencyConflictException(new InvalidOperationException());
        fixture.Delay.Step = TimeSpan.FromSeconds(1);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => fixture.Service.ExpressInterestAsync(fixture.CandidateId, fixture.AddJob(), TestContext.Current.CancellationToken));

        Assert.Equal(JobExplorationService.ConflictRetryLimit, TimeSpan.FromSeconds(5));
        Assert.Equal(6, fixture.Candidates.Loads);
        Assert.Equal(5, fixture.Delay.Attempts.Count);
        Assert.Equal(5, fixture.Store.Discards);
        Assert.Empty(fixture.Store.Committed);
    }

    /// <summary>
    /// Na repetição, a vaga passa a ter resposta e a quota é a atualizada: o segundo de dois interesses simultâneos com a
    /// quota no limite recebe ERR-31 em vez de ultrapassar a quota.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RetryAfterConflictSeesTheUpdatedQuota()
    {
        Candidate reloaded = ExhaustedAt(Now);
        Fixture fixture = new(() => CandidateWith(remaining: 1));
        fixture.Store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        fixture.Store.OnDiscard = () => fixture.Candidates.SetSource(() => reloaded);

        QuotaExhaustedException exception = await Assert.ThrowsAsync<QuotaExhaustedException>(
            () => fixture.Service.ExpressInterestAsync(fixture.CandidateId, fixture.AddJob(), TestContext.Current.CancellationToken));

        Assert.Equal(Now.AddHours(24), exception.BlockedUntil);
        Assert.Empty(fixture.Store.Committed);
    }

    /// <summary>
    /// A consulta repõe a quota vencida e grava; sem reposição não grava.
    /// </summary>
    /// <param name="hoursSinceLastInterest">Horas desde o interesse que esgotou a quota.</param>
    /// <param name="expectedAvailable">Interesses disponíveis esperados.</param>
    /// <param name="expectedSaves">Gravações esperadas.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(25, 10, 1)]
    [InlineData(24, 10, 1)]
    [InlineData(23, 0, 0)]
    public async Task GetQuotaRestoresAndSavesOnlyWhenTheBlockEnded(int hoursSinceLastInterest, int expectedAvailable, int expectedSaves)
    {
        Fixture fixture = new(() => ExhaustedAt(Now.AddHours(-hoursSinceLastInterest)));

        QuotaDto quota = await fixture.Service.GetQuotaAsync(fixture.CandidateId, TestContext.Current.CancellationToken);

        Assert.Equal(expectedAvailable, quota.Available);
        Assert.Equal(Candidate.MaxInterests, quota.Total);
        Assert.Equal(expectedAvailable == 0 ? Now.AddHours(24 - hoursSinceLastInterest) : null, quota.BlockedUntil);
        Assert.Equal(expectedSaves, fixture.Store.Saves);
    }

    /// <summary>
    /// A consulta de uma quota parcial não grava nada.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetQuotaDoesNotSaveWhenNothingWasRestored()
    {
        Fixture fixture = new(() => CandidateWith(remaining: 6));

        QuotaDto quota = await fixture.Service.GetQuotaAsync(fixture.CandidateId, TestContext.Current.CancellationToken);

        Assert.Equal(new QuotaDto(6, 10, null), quota);
        Assert.Equal(0, fixture.Store.Saves);
    }

    /// <summary>
    /// A consulta que grava repete perante conflitos de concorrência, também 30 vezes seguidas, e grava uma só vez.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetQuotaRetriesOnConflictAndSavesOnce()
    {
        Fixture fixture = new(() => ExhaustedAt(Now.AddDays(-2)));
        for (int index = 0; index < 30; index++)
        {
            fixture.Store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        }

        QuotaDto quota = await fixture.Service.GetQuotaAsync(fixture.CandidateId, TestContext.Current.CancellationToken);

        Assert.Equal(new QuotaDto(10, 10, null), quota);
        Assert.Equal(30, fixture.Store.Discards);
        Assert.Equal(1, fixture.Store.Saves);
        Assert.Equal(Enumerable.Range(1, 30), fixture.Delay.Attempts);
    }

    /// <summary>
    /// Se outro pedido repôs a quota entretanto, a repetição da consulta lê-a reposta e não grava outra vez.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetQuotaRetryDoesNotSaveWhenAnotherRequestAlreadyRestored()
    {
        Fixture fixture = new(() => ExhaustedAt(Now.AddDays(-2)));
        fixture.Store.FailNextSaves.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        fixture.Store.OnDiscard = () => fixture.Candidates.SetSource(() => CandidateWith(remaining: 10));

        QuotaDto quota = await fixture.Service.GetQuotaAsync(fixture.CandidateId, TestContext.Current.CancellationToken);

        Assert.Equal(new QuotaDto(10, 10, null), quota);
        Assert.Equal(1, fixture.Store.Discards);
        Assert.Equal(0, fixture.Store.Saves);
    }

    /// <summary>
    /// Quando o limite de tempo acaba, a consulta deixa o <see cref="ConcurrencyConflictException"/> propagar.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetQuotaPropagatesTheConcurrencyConflictWhenTheTimeLimitEnds()
    {
        Fixture fixture = new(() => ExhaustedAt(Now.AddDays(-2)));
        fixture.Store.FailAlways = new ConcurrencyConflictException(new InvalidOperationException());
        fixture.Delay.Step = TimeSpan.FromSeconds(1);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => fixture.Service.GetQuotaAsync(fixture.CandidateId, TestContext.Current.CancellationToken));

        Assert.Equal(6, fixture.Candidates.Loads);
        Assert.Equal(5, fixture.Delay.Attempts.Count);
    }

    /// <summary>
    /// O instante vem sempre do relógio do servidor, lido em cada operação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UsesTheServerClock()
    {
        Fixture fixture = new();

        fixture.Clock.UtcNow = Now.AddHours(3);
        await fixture.Service.DeclineAsync(fixture.CandidateId, fixture.AddJob(), TestContext.Current.CancellationToken);
        fixture.Clock.UtcNow = Now.AddHours(5);
        await fixture.Service.ExpressInterestAsync(fixture.CandidateId, fixture.AddJob(), TestContext.Current.CancellationToken);

        Assert.Equal(
            [Now.AddHours(3), Now.AddHours(5)],
            fixture.Store.Committed.Select(match => match.CandidateActionAt));
    }

    /// <summary>
    /// O cancelamento chega aos repositórios e à gravação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ForwardsTheCancellationToken()
    {
        Fixture fixture = new();
        using var source = new CancellationTokenSource();

        await fixture.Service.ExpressInterestAsync(fixture.CandidateId, fixture.AddJob(), source.Token);

        Assert.Equal(source.Token, fixture.Store.SavedWith);
        Assert.Equal(source.Token, fixture.Candidates.LoadedWith);
    }

    /// <summary>
    /// Um Candidato que não existe é uma anomalia (a conta existe, mas a linha não), não um erro do catálogo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MissingCandidateIsNotMappedToACatalogError()
    {
        Fixture fixture = new();
        fixture.Candidates.SetSource(null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.GetQuotaAsync(fixture.CandidateId, TestContext.Current.CancellationToken));
    }

    private static async Task AssertRejectedAsync(Fixture fixture, bool interest, Guid jobId, JobAnswerRejection expected)
    {
        int committed = fixture.Store.Committed.Count;
        Task action = interest
            ? fixture.Service.ExpressInterestAsync(fixture.CandidateId, jobId, TestContext.Current.CancellationToken)
            : fixture.Service.DeclineAsync(fixture.CandidateId, jobId, TestContext.Current.CancellationToken);

        JobAnswerRejectedException exception = await Assert.ThrowsAsync<JobAnswerRejectedException>(() => action);

        Assert.Equal(expected, exception.Reason);
        Assert.Equal(committed, fixture.Store.Committed.Count);
    }

    private static Candidate NewCandidate()
    {
        var user = new AppUser("candidato@katch.pt", "hash", UserType.Candidate, Now.AddYears(-1), Now.AddYears(-1));
        return new Candidate(user, "Ana Silva", "912345678", Guid.NewGuid());
    }

    private static Candidate CandidateWith(int remaining)
    {
        Candidate candidate = NewCandidate();
        for (int index = Candidate.MaxInterests; index > remaining; index--)
        {
            candidate.ConsumeInterest(Now.AddDays(-5));
        }

        return candidate;
    }

    private static Candidate ExhaustedAt(DateTimeOffset lastInterest)
    {
        Candidate candidate = NewCandidate();
        for (int index = 0; index < Candidate.MaxInterests; index++)
        {
            candidate.ConsumeInterest(lastInterest);
        }

        return candidate;
    }

    private sealed class Fixture
    {
        private readonly Dictionary<Guid, JobWithCompanyStatus> jobs = [];

        public Fixture(Func<Candidate>? candidate = null)
        {
            this.Candidates = new FakeCandidates(candidate ?? NewCandidate);
            this.Store = new FakeStore();
            this.Clock = new FixedClock { UtcNow = Now };
            this.Time = new FakeTimeProvider();
            this.Delay = new FakeRetryDelay(this.Candidates, this.Time);
            this.Service = new JobExplorationService(
                this.Candidates,
                new FakeJobs(this.jobs),
                this.Store,
                this.Store,
                this.Clock,
                this.Time,
                this.Delay);
        }

        public FakeCandidates Candidates { get; }

        public FakeStore Store { get; }

        public FixedClock Clock { get; }

        public FakeTimeProvider Time { get; }

        public FakeRetryDelay Delay { get; }

        public JobExplorationService Service { get; }

        public Guid CandidateId { get; } = Guid.NewGuid();

        public Guid AddJob(
            Action<Job>? configure = null,
            CompanyStatus company = CompanyStatus.Approved,
            DateTimeOffset? expiresAt = null,
            bool publish = true)
        {
            var job = new Job(
                Guid.NewGuid(),
                new JobData
                {
                    Title = "Programador",
                    Description = "Descrição da vaga.",
                    MinSalary = 1000m,
                    MaxSalary = 2000m,
                    LocationId = Guid.NewGuid(),
                    ContractType = ContractType.Permanent,
                    WorkMode = WorkMode.Remote,
                    ExpiresAt = expiresAt,
                });

            if (publish)
            {
                job.Publish(Now.AddDays(-10));
            }

            configure?.Invoke(job);
            this.jobs[job.Id] = new JobWithCompanyStatus(job, company);
            return job.Id;
        }
    }

    private sealed class FakeJobs : IJobRepository
    {
        private readonly Dictionary<Guid, JobWithCompanyStatus> jobs;

        public FakeJobs(Dictionary<Guid, JobWithCompanyStatus> jobs)
        {
            this.jobs = jobs;
        }

        public Task<JobWithCompanyStatus?> GetWithCompanyStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.jobs.GetValueOrDefault(jobId));
        }

        public Task<int> CountFirstPublishedAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeCandidates : ICandidateRepository
    {
        private Func<Candidate>? source;

        public FakeCandidates(Func<Candidate> source)
        {
            this.source = source;
        }

        public int Loads { get; private set; }

        public CancellationToken LoadedWith { get; private set; }

        /// <summary>
        /// Gets the instance returned by the last read, which stands for the entity tracked by the context.
        /// </summary>
        public Candidate? Current { get; private set; }

        /// <summary>
        /// Simula o estado da linha na base de dados: cada leitura devolve uma entidade nova construída a partir dele.
        /// </summary>
        /// <param name="newSource">Estado da linha, ou <see langword="null"/> se o Candidato não existir.</param>
        public void SetSource(Func<Candidate>? newSource)
        {
            this.source = newSource;
        }

        public Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            this.Loads++;
            this.LoadedWith = cancellationToken;
            this.Current = this.source?.Invoke();
            return Task.FromResult(this.Current);
        }

        public Task<List<Candidate>> ListQuotaDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeStore : IMatchRepository, IUnitOfWork
    {
        public List<Match> Pending { get; } = [];

        public List<Match> Committed { get; } = [];

        public Queue<Exception> FailNextSaves { get; } = new();

        public Exception? FailAlways { get; set; }

        public Action? OnDiscard { get; set; }

        public int Saves { get; private set; }

        public int Discards { get; private set; }

        public CancellationToken SavedWith { get; private set; }

        public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Committed.FirstOrDefault(match => match.CandidateId == candidateId && match.JobId == jobId));
        }

        public Task AddAsync(Match match, CancellationToken cancellationToken = default)
        {
            this.Pending.Add(match);
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (this.FailNextSaves.TryDequeue(out Exception? failure))
            {
                throw failure;
            }

            if (this.FailAlways is not null)
            {
                throw this.FailAlways;
            }

            this.Saves++;
            this.SavedWith = cancellationToken;
            int saved = this.Pending.Count;
            this.Committed.AddRange(this.Pending);
            this.Pending.Clear();
            return Task.FromResult(saved);
        }

        public void DiscardChanges()
        {
            this.Discards++;
            this.Pending.Clear();
            this.OnDiscard?.Invoke();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountInterestsAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountMatchesAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private long ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan time)
        {
            this.ticks += time.Ticks;
        }

        public override long GetTimestamp()
        {
            return this.ticks;
        }
    }

    private sealed class FakeRetryDelay : IConflictRetryDelay
    {
        private readonly FakeCandidates candidates;
        private readonly FakeTimeProvider time;

        public FakeRetryDelay(FakeCandidates candidates, FakeTimeProvider time)
        {
            this.candidates = candidates;
            this.time = time;
        }

        /// <summary>
        /// Gets or sets the time that each wait makes pass on the fake time provider, without sleeping.
        /// </summary>
        public TimeSpan Step { get; set; } = TimeSpan.FromMilliseconds(10);

        /// <summary>
        /// Gets the number of reads of the candidate done when each wait was requested.
        /// </summary>
        public List<int> LoadsAtEachWait { get; } = [];

        public List<int> Attempts { get; } = [];

        public Task WaitAsync(int attempt, CancellationToken cancellationToken = default)
        {
            this.Attempts.Add(attempt);
            this.LoadsAtEachWait.Add(this.candidates.Loads);
            this.time.Advance(this.Step);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
