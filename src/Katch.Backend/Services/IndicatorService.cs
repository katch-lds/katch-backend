// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IIndicatorService"/> (modelo de classes do backend v02, secções 4 e 5.1): cada indicador
/// é uma contagem feita na base de dados, sem carregar entidades nem texto de mensagens (RF096, RF103).
/// </summary>
public sealed class IndicatorService : IIndicatorService
{
    /// <summary>
    /// Fuso das datas do período, o do calendário do Administrador (DAPI-07). O armazenamento continua em UTC.
    /// </summary>
    internal static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private readonly IUserRepository users;
    private readonly ICompanyRepository companies;
    private readonly IJobRepository jobs;
    private readonly IMatchRepository matches;
    private readonly IMessageRepository messages;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndicatorService"/> class.
    /// </summary>
    /// <param name="users">Repositório das contas.</param>
    /// <param name="companies">Repositório das Empresas.</param>
    /// <param name="jobs">Repositório das vagas.</param>
    /// <param name="matches">Repositório das respostas às vagas e dos matches.</param>
    /// <param name="messages">Repositório das mensagens.</param>
    /// <param name="clock">Relógio, para a data atual.</param>
    public IndicatorService(
        IUserRepository users,
        ICompanyRepository companies,
        IJobRepository jobs,
        IMatchRepository matches,
        IMessageRepository messages,
        IClock clock)
    {
        this.users = users;
        this.companies = companies;
        this.jobs = jobs;
        this.matches = matches;
        this.messages = messages;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public async Task<IndicatorsDto> GetAsync(DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken = default)
    {
        this.ValidatePeriod(firstDay, lastDay);
        DateTimeOffset periodStart = StartOfDay(firstDay);
        DateTimeOffset periodEnd = StartOfDay(lastDay.AddDays(1));

        // As consultas partilham o contexto do pedido, por isso correm uma de cada vez.
        return new IndicatorsDto(
            ActiveCandidates: await this.users.CountActiveCandidatesAtAsync(periodEnd, cancellationToken),
            ApprovedCompanies: await this.companies.CountByStatusAtAsync(CompanyStatus.Approved, periodEnd, cancellationToken),
            PendingCompanies: await this.companies.CountByStatusAtAsync(CompanyStatus.Pending, periodEnd, cancellationToken),
            JobsFirstPublished: await this.jobs.CountFirstPublishedAsync(periodStart, periodEnd, cancellationToken),
            InterestsExpressed: await this.matches.CountInterestsAsync(periodStart, periodEnd, cancellationToken),
            MatchesConfirmed: await this.matches.CountMatchesAsync(periodStart, periodEnd, cancellationToken),
            ConversationsStarted: await this.messages.CountConversationsStartedAsync(periodStart, periodEnd, cancellationToken));
    }

    /// <summary>
    /// Instante UTC em que começa o dia indicado em Lisboa. O fim do período é o início do dia seguinte, exclusivo, o
    /// que equivale a «até às 23:59:59,999» da documentação sem perder os instantes do último milissegundo.
    /// </summary>
    /// <param name="date">Data do calendário de Lisboa.</param>
    /// <returns>O instante UTC das 00:00 desse dia em Lisboa.</returns>
    internal static DateTimeOffset StartOfDay(DateOnly date)
    {
        // As mudanças de hora em Lisboa são à 01:00 e às 02:00, por isso a meia-noite existe sempre e é única.
        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), Lisbon);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    /// <summary>
    /// Data atual no calendário de Lisboa.
    /// </summary>
    /// <param name="now">Instante atual.</param>
    /// <returns>A data em Lisboa nesse instante.</returns>
    internal static DateOnly Today(DateTimeOffset now)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Lisbon).DateTime);
    }

    private void ValidatePeriod(DateOnly firstDay, DateOnly lastDay)
    {
        // Os campos têm os nomes dos parâmetros da rota (from, to).
        var errors = new List<FieldErrorDto>();

        if (firstDay > lastDay)
        {
            errors.Add(new FieldErrorDto("from", FieldReasons.PeriodStartAfterEnd));
        }

        if (lastDay > Today(this.clock.UtcNow))
        {
            errors.Add(new FieldErrorDto("to", FieldReasons.PeriodEndInFuture));
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }
    }
}
