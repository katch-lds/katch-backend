// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="IJobRepository"/> sobre o <see cref="KatchDbContext"/>.
/// </summary>
public sealed class JobRepository : IJobRepository
{
    /// <summary>
    /// Raio médio da Terra em km, usado na fórmula de Haversine (RF015).
    /// </summary>
    internal const double EarthRadiusKm = 6371.0088;

    private readonly KatchDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobRepository"/> class.
    /// </summary>
    /// <param name="context">Contexto da base de dados do pedido.</param>
    public JobRepository(KatchDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public Task<JobWithCompanyStatus?> GetWithCompanyStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // O filtro global das vagas exclui as eliminadas (DM-09).
        return (from job in this.context.Jobs.AsNoTracking()
                join company in this.context.Companies.AsNoTracking() on job.CompanyId equals company.Id
                where job.Id == jobId
                select new JobWithCompanyStatus(job, company.Status))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Jobs
            .Include(job => job.Skills)
            .Include(job => job.Benefits)
            .FirstOrDefaultAsync(job => job.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        await this.context.Jobs.AddAsync(job, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<Job>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        // O operation_log só recebe inserções e o seu identificador é sequencial, por isso a ordem do registo
        // JOB_CREATED é a da data de criação (DM-22) e ordena na base de dados em qualquer fornecedor, sem ordenar por
        // DateTimeOffset. As vagas sem esse registo (inseridas por SQL) ficam no fim.
        return await this.context.Jobs
            .AsNoTracking()
            .Include(job => job.Skills)
            .Include(job => job.Benefits)
            .Where(job => job.CompanyId == companyId)
            .OrderByDescending(job => this.context.OperationLogs
                .Where(log => log.EntityId == job.Id && log.Operation == OperationType.JobCreated)
                .Min(log => (long?)log.Id) ?? 0)
            .ThenBy(job => job.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> HasInterestsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return this.context.Matches.AnyAsync(
            match => match.JobId == jobId && match.Status != MatchStatus.DeclinedByCandidate,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<EligibleJob>> ListEligibleForCandidateAsync(
        Candidate candidate,
        DateTimeOffset now,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        Location? home = await this.context.Locations.AsNoTracking()
            .SingleOrDefaultAsync(location => location.Id == candidate.LocationId, cancellationToken);
        if (home is null)
        {
            return [];
        }

        // Coordenadas do Candidato em radianos, enviadas como parâmetros; as da vaga são lidas na consulta.
        double homeLatitude = ToRadians((double)home.Latitude);
        double homeLongitude = ToRadians((double)home.Longitude);
        double homeCosine = Math.Cos(homeLatitude);
        Guid candidateId = candidate.Id;

        // O filtro global das vagas exclui as eliminadas (DM-09).
        IQueryable<JobDistance> eligible =
            from job in this.context.Jobs.AsNoTracking()
            join company in this.context.Companies.AsNoTracking() on job.CompanyId equals company.Id
            join location in this.context.Locations.AsNoTracking() on job.LocationId equals location.Id
            where job.Status == JobStatus.Published
                && (job.ExpiresAt == null || job.ExpiresAt > now)
                && company.Status == CompanyStatus.Approved
                && !this.context.Matches.Any(match => match.CandidateId == candidateId && match.JobId == job.Id)
            select new JobDistance
            {
                Job = job,

                // Fórmula de Haversine com o raio médio da Terra (RF015): d = 2R·asin(√a), com
                // a = sin²(Δφ/2) + cos φ1 · cos φ2 · sin²(Δλ/2). O Math.Min evita um asin fora do domínio por
                // arredondamento quando as localidades são quase antípodas.
                Distance = 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(
                    Math.Pow(Math.Sin(((((double)location.Latitude) * Math.PI / 180) - homeLatitude) / 2), 2)
                    + (homeCosine * Math.Cos(((double)location.Latitude) * Math.PI / 180)
                        * Math.Pow(Math.Sin(((((double)location.Longitude) * Math.PI / 180) - homeLongitude) / 2), 2))))),
            };

        if (candidate.MinSalaryExpectation is { } minSalary)
        {
            eligible = eligible.Where(item => item.Job.MaxSalary >= minSalary);
        }

        if (candidate.PreferredWorkModes.Count > 0)
        {
            List<WorkMode> workModes = candidate.PreferredWorkModes;
            eligible = eligible.Where(item => workModes.Contains(item.Job.WorkMode));
        }

        if (candidate.PreferredContractTypes.Count > 0)
        {
            List<ContractType> contractTypes = candidate.PreferredContractTypes;
            eligible = eligible.Where(item => contractTypes.Contains(item.Job.ContractType));
        }

        if (candidate.MaxDistanceKm is { } maxDistance)
        {
            // Compara-se a distância arredondada às unidades, a mesma do cartão: abaixo de máximo + 0,5 km arredonda para o
            // máximo ou menos. As vagas remotas não têm limite de distância (RF014).
            double limit = maxDistance + 0.5;
            eligible = eligible.Where(item => item.Job.WorkMode == WorkMode.Remote || item.Distance < limit);
        }

        // A ordem dos cartões não é fixada pelos requisitos (API v02, 6.3.1): primeiro as vagas urgentes e depois as mais
        // próximas; o identificador desempata, para a ordem ser sempre a mesma.
        List<JobDistance> found = await eligible
            .OrderByDescending(item => item.Job.IsUrgent)
            .ThenBy(item => item.Distance)
            .ThenBy(item => item.Job.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return found
            .Select(item => new EligibleJob(item.Job, (int)Math.Round(item.Distance, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    /// <summary>
    /// Projeção intermédia da consulta das vagas elegíveis: a vaga e a distância ainda por arredondar.
    /// </summary>
    private sealed class JobDistance
    {
        /// <summary>
        /// Gets the vaga.
        /// </summary>
        public required Job Job { get; init; }

        /// <summary>
        /// Gets the distância em km, sem arredondamento.
        /// </summary>
        public required double Distance { get; init; }
    }
}
