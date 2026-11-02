// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.BackgroundJobs;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes do encerramento automático das vagas na data-limite sobre SQLite in-memory: a consulta das vagas expiradas, o
/// <see cref="IJobService.CloseExpiredAsync"/> com o registo no <c>operation_log</c>, a tarefa periódica no arranque real
/// da API e o efeito na resposta do Candidato (RF057, RF112, RF014).
/// </summary>
public sealed class JobExpirationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A consulta devolve as vagas publicadas com a data-limite no instante exato ou antes dele, e só essas: não devolve
    /// as que expiram um tick depois, as sem data-limite, as suspensas, as encerradas nem as eliminadas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListExpiredReturnsOnlyPublishedJobsAtOrPastTheirDeadline()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new KatchWebApplicationFactory();
        Seed seed = await SeedAsync(factory);
        Job atTheLimit = seed.Add(Now, JobStatus.Published);
        Job longAgo = seed.Add(Now.AddDays(-30), JobStatus.Published);
        seed.Add(Now.AddTicks(1), JobStatus.Published);
        seed.Add(null, JobStatus.Published);
        seed.Add(Now.AddDays(-1), JobStatus.Suspended);
        seed.Add(Now.AddDays(-1), JobStatus.Closed);
        Job deleted = seed.Add(Now.AddDays(-1), JobStatus.Published);
        deleted.MarkDeleted(Now.AddDays(-2));
        await seed.SaveAsync(cancellationToken);

        using IServiceScope scope = factory.Services.CreateScope();
        List<Job> expired = await scope.ServiceProvider.GetRequiredService<IJobRepository>().ListExpiredAsync(Now, cancellationToken);

        Assert.Equal(
            new[] { atTheLimit.Id, longAgo.Id }.Order(),
            expired.Select(job => job.Id).Order());
    }

    /// <summary>
    /// O serviço encerra e grava as vagas expiradas, regista cada uma como <c>JOB_CLOSED</c> sem autor e não altera as
    /// outras; a execução seguinte não encontra nada; um tick depois encerra a que estava a um tick da data-limite.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ServiceClosesPersistsAndLogsOnlyTheExpiredJobs()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new KatchWebApplicationFactory();
        factory.Clock.Fixed = Now;
        Seed seed = await SeedAsync(factory);
        Job past = seed.Add(Now.AddHours(-1), JobStatus.Published);
        Job oneTickLeft = seed.Add(Now.AddTicks(1), JobStatus.Published);
        Job future = seed.Add(Now.AddDays(7), JobStatus.Published);
        Job withoutDeadline = seed.Add(null, JobStatus.Published);
        Job suspended = seed.Add(Now.AddHours(-1), JobStatus.Suspended);
        await seed.SaveAsync(cancellationToken);

        Assert.Equal(1, await CloseExpiredAsync(factory, cancellationToken));
        Assert.Equal(0, await CloseExpiredAsync(factory, cancellationToken));

        Assert.Equal(JobStatus.Closed, (await LoadAsync(factory, past.Id, cancellationToken)).Status);
        Assert.Equal(JobStatus.Published, (await LoadAsync(factory, oneTickLeft.Id, cancellationToken)).Status);
        Assert.Equal(JobStatus.Published, (await LoadAsync(factory, future.Id, cancellationToken)).Status);
        Assert.Equal(JobStatus.Published, (await LoadAsync(factory, withoutDeadline.Id, cancellationToken)).Status);
        Assert.Equal(JobStatus.Suspended, (await LoadAsync(factory, suspended.Id, cancellationToken)).Status);

        factory.Clock.Fixed = Now.AddTicks(1);
        Assert.Equal(1, await CloseExpiredAsync(factory, cancellationToken));
        Assert.Equal(JobStatus.Closed, (await LoadAsync(factory, oneTickLeft.Id, cancellationToken)).Status);

        List<OperationLog> logs = await LogsAsync(factory, cancellationToken);
        Assert.Equal(
            new[] { past.Id, oneTickLeft.Id },
            logs.Select(log => log.EntityId));
        Assert.All(logs, log =>
        {
            Assert.Null(log.UserId);
            Assert.Equal(OperationType.JobClosed, log.Operation);
        });
        Assert.Equal([Now, Now.AddTicks(1)], logs.Select(log => log.OccurredAt));
    }

    /// <summary>
    /// Depois do encerramento, a vaga deixa de aceitar respostas dos Candidatos pelo seu estado, mesmo com um relógio
    /// anterior à data-limite (a vaga encerrada não volta a ser apresentada; RF014).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ClosedJobIsNoLongerAvailableToCandidates()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new KatchWebApplicationFactory();
        Seed seed = await SeedAsync(factory);
        Job job = seed.Add(Now, JobStatus.Published);
        Candidate candidate = seed.AddCandidate("candidato.expiracao@katch.pt");
        await seed.SaveAsync(cancellationToken);

        factory.Clock.Fixed = Now;
        Assert.Equal(1, await CloseExpiredAsync(factory, cancellationToken));

        factory.Clock.Fixed = Now.AddHours(-1);
        using IServiceScope scope = factory.Services.CreateScope();
        var exploration = scope.ServiceProvider.GetRequiredService<IJobExplorationService>();
        JobAnswerRejectedException rejected = await Assert.ThrowsAsync<JobAnswerRejectedException>(
            () => exploration.ExpressInterestAsync(candidate.Id, job.Id, cancellationToken));

        Assert.Equal(JobAnswerRejection.Unavailable, rejected.Reason);
    }

    /// <summary>
    /// A tarefa periódica, arrancada com a API, encerra a vaga expirada sem que ninguém faça um pedido.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task HostedWorkerClosesTheExpiredJobWithoutAnyRequest()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new KatchWebApplicationFactory { BackgroundJobsIntervalSeconds = 2 };
        factory.Clock.Fixed = Now;
        Seed seed = await SeedAsync(factory);
        Job expired = seed.Add(Now.AddMinutes(-5), JobStatus.Published);
        Job future = seed.Add(Now.AddDays(1), JobStatus.Published);
        await seed.SaveAsync(cancellationToken);

        Job? closed = null;
        DateTime giveUp = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < giveUp)
        {
            closed = await LoadAsync(factory, expired.Id, cancellationToken);
            if (closed.Status == JobStatus.Closed)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        Assert.NotNull(closed);
        Assert.Equal(JobStatus.Closed, closed.Status);
        Assert.Equal(JobStatus.Published, (await LoadAsync(factory, future.Id, cancellationToken)).Status);
        OperationLog log = Assert.Single(await LogsAsync(factory, cancellationToken));
        Assert.Equal(expired.Id, log.EntityId);
        Assert.Null(log.UserId);
    }

    /// <summary>
    /// A tarefa de encerramento fica registada como serviço de fundo da API.
    /// </summary>
    [Fact]
    public void WorkerIsRegisteredAsAHostedService()
    {
        using var factory = new KatchWebApplicationFactory();

        Assert.Contains(factory.Services.GetServices<IHostedService>(), service => service is JobExpirationWorker);
    }

    private static async Task<Seed> SeedAsync(KatchWebApplicationFactory factory)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var user = new AppUser("recrutador.expiracao@katch.pt", "hash", UserType.Recruiter, Now.AddYears(-1), Now.AddYears(-1));
        var recruiter = new Recruiter(user);
        var location = new Location("Leiria", "Leiria", 39.743620m, -8.807050m);
        var company = new Company(
            recruiter.Id,
            new CompanyRegistration
            {
                CompanyName = "Lumen Software, Lda.",
                TaxId = "509115115",
                Industry = Industry.Technology,
                Address = "Avenida da Liberdade, 100, 2400-000 Leiria",
                LocationId = location.Id,
                ContactEmail = "geral@lumen-exemplo.pt",
                ContactPhone = "244000111",
                ResponsibleName = "Rui Matos",
            },
            Now.AddYears(-1));
        company.Approve();

        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        context.Recruiters.Add(recruiter);
        context.Locations.Add(location);
        context.Companies.Add(company);
        await context.SaveChangesAsync(cancellationToken);
        return new Seed(factory, company.Id, location.Id);
    }

    private static async Task<int> CloseExpiredAsync(KatchWebApplicationFactory factory, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IJobService>().CloseExpiredAsync(cancellationToken);
    }

    private static async Task<Job> LoadAsync(KatchWebApplicationFactory factory, Guid id, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Jobs.IgnoreQueryFilters().AsNoTracking().SingleAsync(job => job.Id == id, cancellationToken);
    }

    private static async Task<List<OperationLog>> LogsAsync(KatchWebApplicationFactory factory, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.OperationLogs.AsNoTracking().OrderBy(log => log.Id).ToListAsync(cancellationToken);
    }

    private sealed class Seed
    {
        private readonly KatchWebApplicationFactory factory;
        private readonly Guid companyId;
        private readonly Guid locationId;
        private readonly List<Job> jobs = [];
        private readonly List<Candidate> candidates = [];

        public Seed(KatchWebApplicationFactory factory, Guid companyId, Guid locationId)
        {
            this.factory = factory;
            this.companyId = companyId;
            this.locationId = locationId;
        }

        public Job Add(DateTimeOffset? expiresAt, JobStatus status)
        {
            var job = new Job(
                this.companyId,
                new JobData
                {
                    Title = "Programador Backend .NET",
                    Description = "Desenvolvimento de APIs REST em ASP.NET Core.",
                    MinSalary = 1600m,
                    MaxSalary = 2100m,
                    LocationId = this.locationId,
                    ContractType = ContractType.Permanent,
                    WorkMode = WorkMode.Remote,
                    ExpiresAt = expiresAt,
                });

            // Publicada antes da data-limite, como na API; o relógio do teste fica depois dela.
            job.Publish(Now.AddDays(-60));
            if (status == JobStatus.Suspended)
            {
                job.Suspend(Now.AddDays(-59));
            }
            else if (status == JobStatus.Closed)
            {
                job.Close();
            }

            this.jobs.Add(job);
            return job;
        }

        public Candidate AddCandidate(string email)
        {
            var user = new AppUser(email, "hash", UserType.Candidate, Now.AddYears(-1), Now.AddYears(-1));
            var candidate = new Candidate(user, "Ana Silva", "912345678", this.locationId);
            this.candidates.Add(candidate);
            return candidate;
        }

        public async Task SaveAsync(CancellationToken cancellationToken)
        {
            using IServiceScope scope = this.factory.Services.CreateScope();
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            context.Jobs.AddRange(this.jobs);
            context.Candidates.AddRange(this.candidates);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
