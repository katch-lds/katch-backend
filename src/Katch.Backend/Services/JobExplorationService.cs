// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IJobExplorationService"/> (modelo de classes do backend v02, secção 5). A ordem das
/// verificações é a mesma na recusa e no interesse: vaga inexistente ou nunca publicada, resposta já existente, vaga já
/// não disponível e, só no interesse, a quota. Nada é acrescentado ao contexto antes de todas passarem.
/// </summary>
public sealed class JobExplorationService : IJobExplorationService
{
    /// <summary>
    /// Nome do índice único de <c>match</c> por Candidato e vaga.
    /// </summary>
    internal const string UniqueAnswerIndex = "uq_match_candidate_job";

    /// <summary>
    /// Tempo total máximo que uma operação que lê e grava o Candidato repete, perante conflitos de concorrência. Passado
    /// este tempo o <see cref="ConcurrencyConflictException"/> sobe (ERR-41), como último recurso.
    /// </summary>
    internal static readonly TimeSpan ConflictRetryLimit = TimeSpan.FromSeconds(5);

    private readonly ICandidateRepository candidates;
    private readonly IJobRepository jobs;
    private readonly IMatchRepository matches;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;
    private readonly TimeProvider timeProvider;
    private readonly IConflictRetryDelay retryDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExplorationService"/> class.
    /// </summary>
    /// <param name="candidates">Repositório dos Candidatos.</param>
    /// <param name="jobs">Repositório das vagas.</param>
    /// <param name="matches">Repositório das respostas às vagas.</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    /// <param name="clock">Relógio do servidor, a única fonte do instante das operações.</param>
    /// <param name="timeProvider">Fonte do tempo decorrido, que limita a repetição por conflito de concorrência.</param>
    /// <param name="retryDelay">Espera entre as repetições por conflito de concorrência.</param>
    public JobExplorationService(
        ICandidateRepository candidates,
        IJobRepository jobs,
        IMatchRepository matches,
        IUnitOfWork unitOfWork,
        IClock clock,
        TimeProvider timeProvider,
        IConflictRetryDelay retryDelay)
    {
        this.candidates = candidates;
        this.jobs = jobs;
        this.matches = matches;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
        this.timeProvider = timeProvider;
        this.retryDelay = retryDelay;
    }

    /// <inheritdoc/>
    public async Task DeclineAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = this.clock.UtcNow;

        await this.EnsureCanAnswerAsync(candidateId, jobId, now, cancellationToken);
        await this.matches.AddAsync(Match.Decline(candidateId, jobId, now), cancellationToken);
        await this.SaveAnswerAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<QuotaDto> ExpressInterestAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
    {
        return this.RetryOnConflictAsync(() => this.TryExpressInterestAsync(candidateId, jobId, cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<QuotaDto> GetQuotaAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        return this.RetryOnConflictAsync(() => this.TryGetQuotaAsync(candidateId, cancellationToken), cancellationToken);
    }

    private static QuotaDto ToQuota(Candidate candidate)
    {
        return new QuotaDto(candidate.SwipeRightsRemaining, Candidate.MaxInterests, candidate.NextSwipeAvailableAt);
    }

    /// <summary>
    /// Repete a operação, desde a leitura, enquanto falhar por conflito de concorrência (<c>xmin</c>) e o tempo total não
    /// passar de <see cref="ConflictRetryLimit"/>. Em cada repetição descarta o contexto e espera um pouco (jitter), para
    /// que os pedidos simultâneos do mesmo Candidato se serializem e cada um leia a quota já atualizada pelos outros.
    /// </summary>
    private async Task<QuotaDto> RetryOnConflictAsync(Func<Task<QuotaDto>> operation, CancellationToken cancellationToken)
    {
        long started = this.timeProvider.GetTimestamp();

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (ConcurrencyConflictException) when (this.timeProvider.GetElapsedTime(started) < ConflictRetryLimit)
            {
                // Outro pedido do mesmo Candidato gravou primeiro: repete-se tudo desde a leitura, com o valor atualizado.
                this.unitOfWork.DiscardChanges();
                await this.retryDelay.WaitAsync(attempt, cancellationToken);
            }
        }
    }

    private async Task<QuotaDto> TryExpressInterestAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.clock.UtcNow;

        await this.EnsureCanAnswerAsync(candidateId, jobId, now, cancellationToken);
        Candidate candidate = await this.GetCandidateAsync(candidateId, cancellationToken);

        // Repõe a quota se o bloqueio terminou e lança QuotaExhaustedException se continua esgotada; nesse caso nada foi
        // acrescentado ao contexto.
        candidate.ConsumeInterest(now);
        await this.matches.AddAsync(Match.ExpressInterest(candidateId, jobId, now), cancellationToken);
        await this.SaveAnswerAsync(cancellationToken);

        return ToQuota(candidate);
    }

    private async Task<QuotaDto> TryGetQuotaAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        Candidate candidate = await this.GetCandidateAsync(candidateId, cancellationToken);

        if (candidate.RestoreQuotaIfDue(this.clock.UtcNow))
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToQuota(candidate);
    }

    private async Task EnsureCanAnswerAsync(Guid candidateId, Guid jobId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        JobWithCompanyStatus? found = await this.jobs.GetWithCompanyStatusAsync(jobId, cancellationToken);

        if (found is null || found.Job.FirstPublishedAt is null)
        {
            throw new JobAnswerRejectedException(JobAnswerRejection.NotFound);
        }

        if (await this.matches.GetAsync(candidateId, jobId, cancellationToken) is not null)
        {
            throw new JobAnswerRejectedException(JobAnswerRejection.AlreadyAnswered);
        }

        // Só a parte de estado do RF014; a distância, o salário e as preferências são do cartão.
        if (!found.Job.IsVisibleToCandidates(now) || found.CompanyStatus != CompanyStatus.Approved)
        {
            throw new JobAnswerRejectedException(JobAnswerRejection.Unavailable);
        }
    }

    private async Task<Candidate> GetCandidateAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        return await this.candidates.GetByIdAsync(candidateId, cancellationToken)
            ?? throw new InvalidOperationException("O Candidato autenticado não existe.");
    }

    private async Task SaveAnswerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueAnswerIndex)
        {
            // Dois pedidos passaram a verificação prévia ao mesmo tempo: o segundo é uma segunda resposta (RF019).
            throw new JobAnswerRejectedException(JobAnswerRejection.AlreadyAnswered);
        }
    }
}
