// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Dados partilhados pelos testes da avaliação dos Candidatos (documentação da API v02, secção 6.6): Recrutadores com
/// Empresa, vagas com competências pretendidas e interesses de Candidatos. Os dados são fictícios.
/// </summary>
internal static class CandidateEvaluationTestData
{
    /// <summary>
    /// Grava um Recrutador com a Empresa no estado indicado e emite a credencial.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="companyStatus">Estado da Empresa; <see langword="null"/> para não registar Empresa.</param>
    /// <returns>O identificador da Empresa (vazio se não houver) e a credencial do Recrutador.</returns>
    internal static async Task<(Guid CompanyId, Guid RecruiterId, string Token)> SeedRecruiterAsync(
        KatchWebApplicationFactory factory,
        CompanyStatus? companyStatus = CompanyStatus.Approved)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(factory, UserType.Recruiter, companyStatus: companyStatus);
        string token = factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;

        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        Guid companyId = await context.Companies.AsNoTracking()
            .Where(company => company.RecruiterId == user.Id)
            .Select(company => company.Id)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        return (companyId, user.Id, token);
    }

    /// <summary>
    /// Grava uma vaga publicada da Empresa, com as competências pretendidas indicadas.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="companyId">Empresa dona da vaga.</param>
    /// <param name="skills">Competências da lista pretendidas na vaga, já gravadas.</param>
    /// <returns>A vaga gravada.</returns>
    internal static async Task<Job> SeedJobAsync(KatchWebApplicationFactory factory, Guid companyId, params Skill[] skills)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        var job = new Job(
            companyId,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        job.Publish(now.AddDays(-10));
        foreach (Skill skill in skills)
        {
            job.Skills.Add(skill);
        }

        await SaveAsync(factory, context =>
        {
            context.Skills.AttachRange(skills);
            context.Locations.Add(location);
            context.Jobs.Add(job);
        });
        return job;
    }

    /// <summary>
    /// Grava uma competência da lista pré-definida.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="name">Designação da competência.</param>
    /// <returns>A competência gravada.</returns>
    internal static async Task<Skill> SeedSkillAsync(KatchWebApplicationFactory factory, string name)
    {
        var skill = new Skill(name);
        await SaveAsync(factory, context => context.Skills.Add(skill));
        return skill;
    }

    /// <summary>
    /// Grava um Candidato novo, com a sua localidade em Braga.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="fullName">Nome completo.</param>
    /// <param name="skills">Competências da lista já gravadas.</param>
    /// <param name="configure">Alteração opcional do Candidato antes de gravar.</param>
    /// <returns>O Candidato e a sua localidade.</returns>
    internal static async Task<(Candidate Candidate, Location Location)> SeedCandidateAsync(
        KatchWebApplicationFactory factory,
        string fullName = "Ana Ferreira",
        IReadOnlyList<Skill>? skills = null,
        Action<Candidate>? configure = null)
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Candidate, created, created);
        var location = new Location($"Guimarães {Guid.NewGuid():N}", "Braga", 41.445m, -8.292m);
        var candidate = new Candidate(user, fullName, "912345678", location.Id);
        foreach (Skill skill in skills ?? [])
        {
            candidate.AddSkill(skill);
        }

        configure?.Invoke(candidate);

        await SaveAsync(factory, context =>
        {
            // As competências já existem: ficam acompanhadas como inalteradas e só a associação é gravada.
            context.Skills.AttachRange(skills ?? []);
            context.Locations.Add(location);
            context.Candidates.Add(candidate);
        });
        return (candidate, location);
    }

    /// <summary>
    /// Grava a resposta do Candidato à vaga, no estado pedido.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="candidateId">Candidato que responde.</param>
    /// <param name="jobId">Vaga.</param>
    /// <param name="status">Estado da resposta.</param>
    /// <param name="answeredAt">Instante da resposta do Candidato.</param>
    /// <param name="recruiterId">Recrutador que decidiu, para os estados de decisão.</param>
    /// <returns>A resposta gravada.</returns>
    internal static async Task<Match> SeedMatchAsync(
        KatchWebApplicationFactory factory,
        Guid candidateId,
        Guid jobId,
        MatchStatus status = MatchStatus.Waiting,
        DateTimeOffset? answeredAt = null,
        Guid? recruiterId = null)
    {
        DateTimeOffset at = answeredAt ?? DateTimeOffset.UtcNow.AddDays(-1);
        Match match = status == MatchStatus.DeclinedByCandidate
            ? Match.Decline(candidateId, jobId, at)
            : Match.ExpressInterest(candidateId, jobId, at);

        if (status is MatchStatus.Matched or MatchStatus.RejectedByRecruiter)
        {
            Guid decidedBy = recruiterId ?? throw new ArgumentNullException(nameof(recruiterId));
            match.OpenProfile(at);
            if (status == MatchStatus.Matched)
            {
                match.Accept(decidedBy, at);
            }
            else
            {
                match.Reject(decidedBy, at);
            }
        }

        await SaveAsync(factory, context => context.Matches.Add(match));
        return match;
    }

    /// <summary>
    /// Lê a resposta da base de dados, sem a acompanhar.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="matchId">Identificador da resposta.</param>
    /// <returns>A resposta gravada.</returns>
    internal static async Task<Match> ReadMatchAsync(KatchWebApplicationFactory factory, Guid matchId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Matches.AsNoTracking()
            .SingleAsync(match => match.Id == matchId, TestContext.Current.CancellationToken);
    }

    private static async Task SaveAsync(KatchWebApplicationFactory factory, Action<KatchDbContext> add)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        add(context);
        await context.SaveChangesAsync(cancellationToken);
    }
}
