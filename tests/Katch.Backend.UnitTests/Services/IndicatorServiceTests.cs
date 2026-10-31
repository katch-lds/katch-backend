// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;
using Katch.Backend.Services;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes de <see cref="IndicatorService"/>: validação do período, conversão das datas de Lisboa para instantes UTC e
/// correspondência de cada contagem ao indicador do RF096 (documentação da API v02, secção 6.12.1; DAPI-07). As
/// contagens sobre dados conhecidos são verificadas nos testes de integração.
/// </summary>
public sealed class IndicatorServiceTests
{
    // 2026-10-29 12:00 em Lisboa (hora de inverno, UTC+0, desde 2026-10-25).
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeCounts counts = new();
    private readonly FixedClock clock = new(Now);

    /// <summary>
    /// Cada contagem vai para o seu indicador; os três primeiros usam o fim do período e os outros quatro o período
    /// inteiro, das 00:00 do primeiro dia às 00:00 do dia seguinte ao último, em Lisboa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EachCountGoesToItsIndicator()
    {
        IndicatorsDto indicators = await this.Service().GetAsync(
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 9),
            TestContext.Current.CancellationToken);

        Assert.Equal(new IndicatorsDto(1, 2, 3, 4, 5, 6, 7), indicators);

        // Em outubro, antes de 2026-10-25, Lisboa está em UTC+1.
        var start = new DateTimeOffset(2026, 9, 30, 23, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 9, 23, 0, 0, TimeSpan.Zero);
        Assert.Equal(end, this.counts.ActiveCandidatesAt);
        Assert.Equal([(CompanyStatus.Approved, end), (CompanyStatus.Pending, end)], this.counts.CompanyStatusQueries);
        Assert.Equal((start, end), this.counts.FirstPublishedPeriod);
        Assert.Equal((start, end), this.counts.InterestsPeriod);
        Assert.Equal((start, end), this.counts.MatchesPeriod);
        Assert.Equal((start, end), this.counts.ConversationsPeriod);
    }

    /// <summary>
    /// Um período de um só dia, que é o dia atual, é válido.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SingleDayEndingTodayIsAccepted()
    {
        var today = new DateOnly(2026, 10, 29);

        await this.Service().GetAsync(today, today, TestContext.Current.CancellationToken);

        Assert.Equal(new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero), this.counts.InterestsPeriod!.Value.Start);
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero), this.counts.InterestsPeriod!.Value.End);
    }

    /// <summary>
    /// A data inicial posterior à final é recusada com o campo <c>from</c>, sem consultar a base de dados.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task StartAfterEndIsRejected()
    {
        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().GetAsync(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 9), TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("from", FieldReasons.PeriodStartAfterEnd)], exception.Fields);
        Assert.Null(this.counts.ActiveCandidatesAt);
    }

    /// <summary>
    /// A data final posterior à data atual em Lisboa é recusada com o campo <c>to</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EndAfterTodayIsRejected()
    {
        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().GetAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 30), TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("to", FieldReasons.PeriodEndInFuture)], exception.Fields);
        Assert.Null(this.counts.ActiveCandidatesAt);
    }

    /// <summary>
    /// Os dois erros do período são indicados na mesma resposta.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BothPeriodErrorsAreReportedTogether()
    {
        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().GetAsync(new DateOnly(2026, 12, 31), new DateOnly(2026, 12, 1), TestContext.Current.CancellationToken));

        Assert.Equal(
            [
                new FieldErrorDto("from", FieldReasons.PeriodStartAfterEnd),
                new FieldErrorDto("to", FieldReasons.PeriodEndInFuture),
            ],
            exception.Fields);
    }

    /// <summary>
    /// A data atual é a de Lisboa: às 23:30 UTC de um dia de verão já é o dia seguinte em Lisboa, e esse dia pode ser a
    /// data final.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TodayIsTheLisbonDate()
    {
        this.clock.UtcNow = new DateTimeOffset(2026, 7, 14, 23, 30, 0, TimeSpan.Zero);

        await this.Service().GetAsync(new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 15), TestContext.Current.CancellationToken);

        Assert.Equal(new DateTimeOffset(2026, 7, 15, 23, 0, 0, TimeSpan.Zero), this.counts.ActiveCandidatesAt);
    }

    /// <summary>
    /// O início do dia em Lisboa: UTC+0 no inverno, UTC+1 no verão, e o dia da mudança de hora (23 h) e o seu dia
    /// seguinte começam à meia-noite local.
    /// </summary>
    /// <param name="date">Data do calendário de Lisboa.</param>
    /// <param name="expectedUtc">Instante UTC esperado das 00:00 locais.</param>
    [Theory]
    [InlineData("2026-01-15", "2026-01-15T00:00:00Z")]
    [InlineData("2026-07-15", "2026-07-14T23:00:00Z")]
    [InlineData("2026-03-29", "2026-03-29T00:00:00Z")]
    [InlineData("2026-03-30", "2026-03-29T23:00:00Z")]
    [InlineData("2026-10-25", "2026-10-24T23:00:00Z")]
    [InlineData("2026-10-26", "2026-10-26T00:00:00Z")]
    public void StartOfDayUsesLisbonTime(string date, string expectedUtc)
    {
        DateTimeOffset start = IndicatorService.StartOfDay(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(DateTimeOffset.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture), start);
        Assert.Equal(TimeSpan.Zero, start.Offset);
    }

    private IndicatorService Service()
    {
        return new IndicatorService(this.counts, this.counts, this.counts, this.counts, this.counts, this.clock);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now)
        {
            this.UtcNow = now;
        }

        public DateTimeOffset UtcNow { get; set; }
    }

    /// <summary>
    /// Devolve um valor diferente por indicador (1 a 7, pela ordem do <see cref="IndicatorsDto"/>) e regista os
    /// argumentos, para detetar contagens trocadas.
    /// </summary>
    private sealed class FakeCounts : IUserRepository, ICompanyRepository, IJobRepository, IMatchRepository, IMessageRepository
    {
        public DateTimeOffset? ActiveCandidatesAt { get; private set; }

        public List<(CompanyStatus Status, DateTimeOffset At)> CompanyStatusQueries { get; } = [];

        public (DateTimeOffset Start, DateTimeOffset End)? FirstPublishedPeriod { get; private set; }

        public (DateTimeOffset Start, DateTimeOffset End)? InterestsPeriod { get; private set; }

        public (DateTimeOffset Start, DateTimeOffset End)? MatchesPeriod { get; private set; }

        public (DateTimeOffset Start, DateTimeOffset End)? ConversationsPeriod { get; private set; }

        public Task<int> CountActiveCandidatesAtAsync(DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            this.ActiveCandidatesAt = at;
            return Task.FromResult(1);
        }

        public Task<int> CountByStatusAtAsync(CompanyStatus status, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            this.CompanyStatusQueries.Add((status, at));
            return Task.FromResult(status == CompanyStatus.Approved ? 2 : 3);
        }

        public Task<int> CountFirstPublishedAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
        {
            this.FirstPublishedPeriod = (periodStart, periodEnd);
            return Task.FromResult(4);
        }

        public Task<int> CountInterestsAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
        {
            this.InterestsPeriod = (periodStart, periodEnd);
            return Task.FromResult(5);
        }

        public Task<int> CountMatchesAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
        {
            this.MatchesPeriod = (periodStart, periodEnd);
            return Task.FromResult(6);
        }

        public Task<int> CountConversationsStartedAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default)
        {
            this.ConversationsPeriod = (periodStart, periodEnd);
            return Task.FromResult(7);
        }

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddRecruiterAsync(Recruiter recruiter, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<JobWithCompanyStatus?> GetWithCompanyStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Match match, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
