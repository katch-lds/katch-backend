// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IQuotaService"/> (modelo de classes do backend v02, secções 5.1 e 5.3).
/// </summary>
public sealed class QuotaService : IQuotaService
{
    private readonly ICandidateRepository candidates;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuotaService"/> class.
    /// </summary>
    /// <param name="candidates">Repositório dos Candidatos.</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    /// <param name="clock">Relógio do servidor, a única fonte do instante da verificação.</param>
    public QuotaService(ICandidateRepository candidates, IUnitOfWork unitOfWork, IClock clock)
    {
        this.candidates = candidates;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public async Task<int> RestoreDueQuotasAsync(CancellationToken cancellationToken = default)
    {
        // O instante vem sempre do relógio do servidor, nunca do cliente, para que ninguém antecipe o fim do bloqueio.
        DateTimeOffset now = this.clock.UtcNow;

        List<Candidate> due = await this.candidates.ListQuotaDueAsync(now, cancellationToken);

        // A regra é a de Candidate.RestoreQuotaIfDue, a mesma da consulta e do interesse: a reposição é sempre integral.
        int restored = due.Count(candidate => candidate.RestoreQuotaIfDue(now));

        if (restored > 0)
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return restored;
    }
}
