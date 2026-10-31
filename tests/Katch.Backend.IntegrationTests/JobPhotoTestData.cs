// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Dados dos testes do carregamento das fotografias da vaga: Recrutadores com Empresa aprovada e vagas nos vários
/// estados. Os dados são fictícios.
/// </summary>
internal static class JobPhotoTestData
{
    private static long taxIdCounter = 400_000_000;

    /// <summary>
    /// Grava um Recrutador com a Empresa aprovada e emite a credencial de sessão.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <returns>A Empresa, a localidade, a competência e a credencial.</returns>
    internal static async Task<RecruiterSeed> SeedRecruiterAsync(KatchWebApplicationFactory factory)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Recruiter, now, now);
        var recruiter = new Recruiter(user);
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        var company = new Company(
            recruiter.Id,
            new CompanyRegistration
            {
                CompanyName = "Lumen Software, Lda.",
                TaxId = Interlocked.Increment(ref taxIdCounter).ToString(CultureInfo.InvariantCulture),
                Industry = Industry.Technology,
                Address = "Rua de Exemplo, 1, 4700-000 Braga",
                LocationId = location.Id,
                ContactEmail = "geral@lumen-exemplo.pt",
                ContactPhone = "253000000",
                ResponsibleName = "Responsável Exemplo",
            },
            now);
        company.Approve();
        var skill = new Skill($"C# {Guid.NewGuid():N}");

        await SaveAsync(factory, context =>
        {
            context.Locations.Add(location);
            context.Users.Add(user);
            context.Recruiters.Add(recruiter);
            context.Companies.Add(company);
            context.Skills.Add(skill);
        });
        return new RecruiterSeed(user, company, location, skill, IssueToken(factory, user));
    }

    /// <summary>
    /// Grava uma vaga da Empresa no estado indicado.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="seed">Recrutador e Empresa da vaga.</param>
    /// <param name="status">Estado da vaga.</param>
    /// <param name="deleted">Se a vaga está eliminada.</param>
    /// <param name="photos">Número de fotografias que a vaga já tem.</param>
    /// <returns>O identificador da vaga.</returns>
    internal static async Task<Guid> SeedJobAsync(
        KatchWebApplicationFactory factory,
        RecruiterSeed seed,
        JobStatus status = JobStatus.Unpublished,
        bool deleted = false,
        int photos = 0)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var job = new Job(
            seed.Company.Id,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = seed.Location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        job.Skills.Add(seed.Skill);
        for (int index = 0; index < photos; index++)
        {
            job.AddPhoto($"job_photo/{index:x32}.jpg");
        }

        switch (status)
        {
            case JobStatus.Published:
                job.Publish(now);
                break;
            case JobStatus.Suspended:
                job.Publish(now);
                job.Suspend(now);
                break;
            case JobStatus.Closed:
                job.Close();
                break;
            default:
                break;
        }

        if (deleted)
        {
            job.MarkDeleted(now);
        }

        await SaveAsync(factory, context =>
        {
            context.Attach(seed.Skill);
            context.Jobs.Add(job);
        });
        return job.Id;
    }

    /// <summary>
    /// Lê as fotografias da vaga, pela ordem guardada, mesmo que a vaga esteja eliminada.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <returns>Os caminhos guardados em <c>photo_paths</c>.</returns>
    internal static async Task<List<string>> ReadPhotoPathsAsync(KatchWebApplicationFactory factory, Guid jobId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        Job job = await context.Jobs.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(item => item.Id == jobId, TestContext.Current.CancellationToken);
        return job.PhotoPaths;
    }

    /// <summary>
    /// Lista os ficheiros de fotografias de vagas que existem no armazenamento da aplicação de teste.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <returns>Os nomes dos ficheiros, ordenados.</returns>
    internal static List<string> StoredPhotos(KatchWebApplicationFactory factory)
    {
        string folder = Path.Combine(factory.FileStorageRoot, "job_photo");
        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToList()
            : [];
    }

    private static string IssueToken(KatchWebApplicationFactory factory, AppUser user)
    {
        return factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
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

    /// <summary>
    /// Recrutador com a Empresa aprovada, a localidade e a competência das vagas, e a credencial de sessão.
    /// </summary>
    /// <param name="User">Conta do Recrutador.</param>
    /// <param name="Company">Empresa aprovada.</param>
    /// <param name="Location">Localidade das vagas.</param>
    /// <param name="Skill">Competência das vagas.</param>
    /// <param name="Token">Credencial de sessão.</param>
    internal sealed record RecruiterSeed(AppUser User, Company Company, Location Location, Skill Skill, string Token);
}
