// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de persistência de <see cref="Job"/> sobre SQLite in-memory: gravação e leitura, filtro global das vagas
/// eliminadas e chaves estrangeiras. Os <c>CHECK</c>, os tipos enumerados e o array <c>varchar(500)[]</c> são
/// específicos do PostgreSQL e estão verificados nos testes unitários do mapeamento e na evidência da migration.
/// </summary>
public sealed class JobPersistenceTests : IClassFixture<KatchWebApplicationFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobPersistenceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public JobPersistenceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// A vaga publicada é gravada com os dados, as fotografias pela ordem e as datas de publicação, e lida de volta igual.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task JobIsSavedAndReadBack()
    {
        (Company company, Location location) = await this.SaveCompanyAndLocationAsync("vaga@katch.pt", "509400001");
        var job = new Job(company.Id, Data(location.Id) with { IsUrgent = true, ExpiresAt = Now.AddDays(30) });
        job.AddPhoto("job-photos/b.jpg");
        job.AddPhoto("job-photos/a.jpg");
        job.Publish(Now);
        await this.SaveAsync(job);

        Job saved = await this.ReadAsync(job.Id, ignoreFilters: false);

        Assert.Equal(company.Id, saved.CompanyId);
        Assert.Equal("Programador Backend .NET", saved.Title);
        Assert.Equal("Desenvolvimento de APIs REST em ASP.NET Core.", saved.Description);
        Assert.Equal(1600.50m, saved.MinSalary);
        Assert.Equal(2100m, saved.MaxSalary);
        Assert.Equal(location.Id, saved.LocationId);
        Assert.Equal(ContractType.Permanent, saved.ContractType);
        Assert.Equal(WorkMode.Remote, saved.WorkMode);
        Assert.True(saved.IsUrgent);
        Assert.Equal(["job-photos/b.jpg", "job-photos/a.jpg"], saved.PhotoPaths);
        Assert.Equal(Now.AddDays(30), saved.ExpiresAt);
        Assert.Equal(JobStatus.Published, saved.Status);
        Assert.Equal(Now, saved.FirstPublishedAt);
        Assert.Equal(Now, saved.PublishedAt);
        Assert.Null(saved.SuspendedAt);
        Assert.Null(saved.DeletedAt);
    }

    /// <summary>
    /// Uma vaga eliminada deixa de aparecer nas consultas (filtro global sobre <c>deleted_at</c>, DM-09), mas a linha
    /// mantém-se na tabela.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DeletedJobIsExcludedFromQueries()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (Company company, Location location) = await this.SaveCompanyAndLocationAsync("eliminada@katch.pt", "509400002");
        var kept = new Job(company.Id, Data(location.Id));
        var deleted = new Job(company.Id, Data(location.Id) with { Title = "Vaga eliminada" });
        await this.SaveAsync(kept);
        await this.SaveAsync(deleted);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Job stored = await context.Jobs.SingleAsync(item => item.Id == deleted.Id, cancellationToken);
            stored.MarkDeleted(Now);
            await context.SaveChangesAsync(cancellationToken);
        }

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            List<Guid> visible = await context.Jobs
                .Where(item => item.CompanyId == company.Id)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);

            Assert.Equal([kept.Id], visible);
        }

        Job row = await this.ReadAsync(deleted.Id, ignoreFilters: true);
        Assert.Equal(Now, row.DeletedAt);
    }

    /// <summary>
    /// Uma vaga com uma Empresa ou uma localidade inexistentes é rejeitada pelas chaves estrangeiras.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownCompanyOrLocationIsRejectedByTheForeignKeys()
    {
        (Company company, Location location) = await this.SaveCompanyAndLocationAsync("fk-vaga@katch.pt", "509400003");

        await Assert.ThrowsAsync<DbUpdateException>(() => this.SaveAsync(new Job(Guid.NewGuid(), Data(location.Id))));
        await Assert.ThrowsAsync<DbUpdateException>(() => this.SaveAsync(new Job(company.Id, Data(Guid.NewGuid()))));
    }

    private static JobData Data(Guid locationId)
    {
        return new JobData
        {
            Title = "Programador Backend .NET",
            Description = "Desenvolvimento de APIs REST em ASP.NET Core.",
            MinSalary = 1600.50m,
            MaxSalary = 2100m,
            LocationId = locationId,
            ContractType = ContractType.Permanent,
            WorkMode = WorkMode.Remote,
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

    private async Task SaveAsync(Job job)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        context.Jobs.Add(job);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Job> ReadAsync(Guid jobId, bool ignoreFilters)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        IQueryable<Job> jobs = ignoreFilters ? context.Jobs.IgnoreQueryFilters() : context.Jobs;
        return await jobs
            .AsNoTracking()
            .SingleAsync(item => item.Id == jobId, TestContext.Current.CancellationToken);
    }
}
