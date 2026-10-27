// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de persistência das listas pré-definidas e das associações sobre SQLite in-memory: <see cref="Skill"/>,
/// <see cref="Benefit"/>, <see cref="CandidateCustomSkill"/> e as tabelas <c>candidate_skill</c>, <c>job_skill</c> e
/// <c>job_benefit</c>. Os <c>CHECK</c> e os índices únicos sobre expressões são específicos do PostgreSQL e estão
/// verificados nos testes unitários do mapeamento e na migration.
/// </summary>
public sealed class ReferenceListPersistenceTests : IClassFixture<KatchWebApplicationFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 17, 0, 0, TimeSpan.Zero);

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceListPersistenceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public ReferenceListPersistenceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// O Candidato é gravado com competências da lista e competências «Outro», e lido de volta com as duas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CandidateIsSavedAndReadBackWithBothKindsOfSkills()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var csharp = new Skill("C# (competências)");
        var sql = new Skill("SQL (competências)");
        Candidate candidate = NewCandidate("competencias@katch.pt", out Location location);
        candidate.AddSkill(csharp);
        candidate.AddSkill(sql);
        candidate.AddCustomSkill("Kotlin");

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            context.Skills.AddRange(csharp, sql);
            context.Locations.Add(location);
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Candidate saved = await context.Candidates
                .Include(item => item.Skills)
                .Include(item => item.CustomSkills)
                .SingleAsync(item => item.Id == candidate.Id, cancellationToken);

            Assert.Equal(
                ["C# (competências)", "SQL (competências)"],
                saved.Skills.Select(skill => skill.Name).Order(StringComparer.Ordinal).ToArray());
            CandidateCustomSkill custom = Assert.Single(saved.CustomSkills);
            Assert.Equal("Kotlin", custom.Label);
            Assert.Equal(candidate.Id, custom.CandidateId);
        }
    }

    /// <summary>
    /// Retirar uma competência da lista e uma competência «Outro» de um Candidato lido da base de dados apaga as linhas
    /// de <c>candidate_skill</c> e de <c>candidate_custom_skill</c>, mas não a competência da lista.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RemovingSkillsDeletesOnlyTheAssociationRows()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var docker = new Skill("Docker (retirar)");
        Candidate candidate = NewCandidate("retirar@katch.pt", out Location location);
        candidate.AddSkill(docker);
        CandidateCustomSkill custom = candidate.AddCustomSkill("Terraform");

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            context.Skills.Add(docker);
            context.Locations.Add(location);
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Candidate stored = await context.Candidates
                .Include(item => item.Skills)
                .Include(item => item.CustomSkills)
                .SingleAsync(item => item.Id == candidate.Id, cancellationToken);

            stored.RemoveSkill(docker.Id);
            stored.RemoveCustomSkill(custom.Id);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Candidate saved = await context.Candidates
                .Include(item => item.Skills)
                .Include(item => item.CustomSkills)
                .SingleAsync(item => item.Id == candidate.Id, cancellationToken);

            Assert.Empty(saved.Skills);
            Assert.Empty(saved.CustomSkills);
            Assert.True(await context.Skills.AnyAsync(skill => skill.Id == docker.Id, cancellationToken));
        }
    }

    /// <summary>
    /// A vaga é gravada com as competências pretendidas e os benefícios, e lida de volta com os dois.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task JobIsSavedAndReadBackWithSkillsAndBenefits()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var java = new Skill("Java (vaga)");
        var insurance = new Benefit("Seguro de saúde (vaga)");
        var remote = new Benefit("Teletrabalho (vaga)");
        (Company company, Location location) = await this.SaveCompanyAndLocationAsync("vaga-listas@katch.pt", "509500001");
        var job = new Job(company.Id, JobDataFor(location.Id));
        job.Skills.Add(java);
        job.Benefits.Add(insurance);
        job.Benefits.Add(remote);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            context.Skills.Add(java);
            context.Benefits.AddRange(insurance, remote);
            context.Jobs.Add(job);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Job saved = await context.Jobs
                .Include(item => item.Skills)
                .Include(item => item.Benefits)
                .SingleAsync(item => item.Id == job.Id, cancellationToken);

            Assert.Equal("Java (vaga)", Assert.Single(saved.Skills).Name);
            Assert.Equal(
                ["Seguro de saúde (vaga)", "Teletrabalho (vaga)"],
                saved.Benefits.Select(benefit => benefit.Name).Order(StringComparer.Ordinal).ToArray());
        }
    }

    /// <summary>
    /// A mesma competência pode ser pretendida por várias vagas e associada a vários Candidatos (relações N:M).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SameSkillIsSharedByCandidatesAndJobs()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var python = new Skill("Python (partilhada)");
        (Company company, Location location) = await this.SaveCompanyAndLocationAsync("partilhada@katch.pt", "509500002");
        var first = new Job(company.Id, JobDataFor(location.Id));
        var second = new Job(company.Id, JobDataFor(location.Id) with { Title = "Analista de dados" });
        first.Skills.Add(python);
        second.Skills.Add(python);
        Candidate candidate = NewCandidate("partilhada-candidato@katch.pt", out Location candidateLocation);
        candidate.AddSkill(python);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            context.Skills.Add(python);
            context.Jobs.AddRange(first, second);
            context.Locations.Add(candidateLocation);
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();

            Assert.Equal(
                2,
                await context.Jobs.CountAsync(item => item.Skills.Any(skill => skill.Id == python.Id), cancellationToken));
            Assert.Equal(
                1,
                await context.Candidates.CountAsync(item => item.Skills.Any(skill => skill.Id == python.Id), cancellationToken));
        }
    }

    /// <summary>
    /// Associar uma competência que não existe na lista é rejeitado pela chave estrangeira de <c>candidate_skill</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownSkillIsRejectedByTheForeignKey()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Candidate candidate = NewCandidate("sem-lista@katch.pt", out Location location);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            context.Locations.Add(location);
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Candidate stored = await context.Candidates
                .Include(item => item.Skills)
                .SingleAsync(item => item.Id == candidate.Id, cancellationToken);

            // A competência não é acrescentada ao contexto como nova: fica como existente, sem linha em skill.
            var unknown = new Skill("Inexistente");
            context.Attach(unknown);
            stored.AddSkill(unknown);

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
        }
    }

    private static Candidate NewCandidate(string email, out Location location)
    {
        location = new Location("Local " + email, "Leiria", 39.743620m, -8.807050m);
        var user = new AppUser(email, "hash", UserType.Candidate, Now, Now);
        return new Candidate(user, "Ana Silva", "912345678", location.Id);
    }

    private static JobData JobDataFor(Guid locationId)
    {
        return new JobData
        {
            Title = "Programador .NET",
            Description = "Desenvolvimento de APIs REST.",
            MinSalary = 1200m,
            MaxSalary = 1800m,
            LocationId = locationId,
            ContractType = ContractType.Permanent,
            WorkMode = WorkMode.Hybrid,
        };
    }

    private async Task<(Company Company, Location Location)> SaveCompanyAndLocationAsync(string email, string taxId)
    {
        var user = new AppUser(email, "hash", UserType.Recruiter, Now, Now);
        var recruiter = new Recruiter(user);
        var location = new Location("Braga " + email, "Braga", 41.545450m, -8.426507m);
        var company = new Company(
            recruiter.Id,
            new CompanyRegistration
            {
                CompanyName = "Lumen Software, Lda.",
                TaxId = taxId,
                Industry = Industry.Technology,
                Address = "Avenida da Liberdade, 100, 4710-000 Braga",
                LocationId = location.Id,
                ContactEmail = "geral@lumen-exemplo.pt",
                ContactPhone = "253000111",
                ResponsibleName = "Rui Matos",
            },
            Now);
        company.Approve();

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        context.Recruiters.Add(recruiter);
        context.Locations.Add(location);
        context.Companies.Add(company);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (company, location);
    }
}
