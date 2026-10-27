// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes de <see cref="QuotaService"/>: reposição integral das quotas cujo período de bloqueio terminou (RF021, P11),
/// com o instante do relógio do servidor fixado em cada teste.
/// </summary>
public sealed class QuotaServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Repõe a quota de todos os Candidatos cujo bloqueio terminou, no limite exato e depois dele, numa só gravação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RestoresTheFullQuotaOfEveryCandidateWhoseBlockEnded()
    {
        Candidate atTheLimit = ExhaustedAt(Now.AddHours(-24));
        Candidate oneHourAfter = ExhaustedAt(Now.AddHours(-25));
        Candidate longAfter = ExhaustedAt(Now.AddDays(-30));
        var (service, candidates, unitOfWork) = Create([atTheLimit, oneHourAfter, longAfter]);

        int restored = await service.RestoreDueQuotasAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, restored);
        Assert.All(
            new[] { atTheLimit, oneHourAfter, longAfter },
            candidate =>
            {
                Assert.Equal(Candidate.MaxInterests, candidate.SwipeRightsRemaining);
                Assert.Null(candidate.NextSwipeAvailableAt);
            });
        Assert.Equal(1, unitOfWork.Saves);
        Assert.Equal(Now, candidates.QueriedAt);
    }

    /// <summary>
    /// Sem quotas vencidas devolve 0 e não grava nada.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DoesNotSaveWhenNoQuotaIsDue()
    {
        var (service, _, unitOfWork) = Create([]);

        int restored = await service.RestoreDueQuotasAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, restored);
        Assert.Equal(0, unitOfWork.Saves);
    }

    /// <summary>
    /// A regra aplicada é a do Candidato: um bloqueio que ainda não terminou (mesmo por um tick) e uma quota não esgotada
    /// nunca são repostos, ainda que o repositório os devolva.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AppliesTheCandidateRuleEvenIfTheRepositoryReturnsAnUndueCandidate()
    {
        Candidate oneTickLeft = ExhaustedAt(Now.AddHours(-24).AddTicks(1));
        Candidate notExhausted = CreateCandidate();
        notExhausted.ConsumeInterest(Now.AddDays(-5));
        Candidate due = ExhaustedAt(Now.AddHours(-24));
        var (service, _, unitOfWork) = Create([oneTickLeft, notExhausted, due]);

        int restored = await service.RestoreDueQuotasAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, restored);
        Assert.Equal(0, oneTickLeft.SwipeRightsRemaining);
        Assert.Equal(Now.AddTicks(1), oneTickLeft.NextSwipeAvailableAt);
        Assert.Equal(9, notExhausted.SwipeRightsRemaining);
        Assert.Equal(Candidate.MaxInterests, due.SwipeRightsRemaining);
        Assert.Equal(1, unitOfWork.Saves);
    }

    /// <summary>
    /// Se nenhum dos devolvidos está vencido, não grava.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DoesNotSaveWhenNoneOfTheReturnedCandidatesIsDue()
    {
        var (service, _, unitOfWork) = Create([ExhaustedAt(Now.AddHours(-23))]);

        int restored = await service.RestoreDueQuotasAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, restored);
        Assert.Equal(0, unitOfWork.Saves);
    }

    /// <summary>
    /// O instante da verificação é sempre o do relógio do servidor, lido em cada execução.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UsesTheServerClockOnEveryRun()
    {
        var (service, candidates, _, clock) = CreateWithClock([]);

        await service.RestoreDueQuotasAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Now, candidates.QueriedAt);

        clock.UtcNow = Now.AddMinutes(1);
        await service.RestoreDueQuotasAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Now.AddMinutes(1), candidates.QueriedAt);
    }

    /// <summary>
    /// O cancelamento chega ao repositório e à gravação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ForwardsTheCancellationToken()
    {
        var (service, candidates, unitOfWork) = Create([ExhaustedAt(Now.AddDays(-2))]);
        using var source = new CancellationTokenSource();

        await service.RestoreDueQuotasAsync(source.Token);

        Assert.Equal(source.Token, candidates.QueriedWith);
        Assert.Equal(source.Token, unitOfWork.SavedWith);
    }

    /// <summary>
    /// Uma falha na gravação (por exemplo um conflito de concorrência com um interesse no mesmo instante) não é engolida
    /// aqui: sobe à tarefa periódica, que a regista e repete.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SaveFailureIsNotSwallowed()
    {
        var (service, _, unitOfWork) = Create([ExhaustedAt(Now.AddDays(-2))]);
        unitOfWork.FailWith = new InvalidOperationException("falha");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RestoreDueQuotasAsync(TestContext.Current.CancellationToken));
    }

    private static (QuotaService Service, FakeCandidates Candidates, FakeUnitOfWork UnitOfWork) Create(List<Candidate> due)
    {
        (QuotaService service, FakeCandidates candidates, FakeUnitOfWork unitOfWork, _) = CreateWithClock(due);
        return (service, candidates, unitOfWork);
    }

    private static (QuotaService Service, FakeCandidates Candidates, FakeUnitOfWork UnitOfWork, FixedClock Clock) CreateWithClock(
        List<Candidate> due)
    {
        var candidates = new FakeCandidates(due);
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FixedClock { UtcNow = Now };
        return (new QuotaService(candidates, unitOfWork, clock), candidates, unitOfWork, clock);
    }

    private static Candidate CreateCandidate()
    {
        var user = new AppUser("candidato@katch.pt", "hash", UserType.Candidate, Now.AddYears(-1), Now.AddYears(-1));
        return new Candidate(user, "Ana Silva", "912345678", Guid.NewGuid());
    }

    private static Candidate ExhaustedAt(DateTimeOffset lastInterest)
    {
        Candidate candidate = CreateCandidate();
        for (int index = 0; index < Candidate.MaxInterests; index++)
        {
            candidate.ConsumeInterest(lastInterest);
        }

        return candidate;
    }

    private sealed class FakeCandidates : ICandidateRepository
    {
        private readonly List<Candidate> due;

        public FakeCandidates(List<Candidate> due)
        {
            this.due = due;
        }

        public DateTimeOffset? QueriedAt { get; private set; }

        public CancellationToken QueriedWith { get; private set; }

        public Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Candidate>> ListQuotaDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            this.QueriedAt = now;
            this.QueriedWith = cancellationToken;
            return Task.FromResult(this.due);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public CancellationToken SavedWith { get; private set; }

        public Exception? FailWith { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (this.FailWith is not null)
            {
                throw this.FailWith;
            }

            this.Saves++;
            this.SavedWith = cancellationToken;
            return Task.FromResult(1);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
