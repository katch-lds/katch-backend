// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Repositories.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Contexto do Entity Framework Core da base de dados <c>katch</c> e unidade de trabalho dos repositórios
/// (modelo de classes do backend, secção 4). As entidades são acrescentadas pelas Issues de cada tabela.
/// </summary>
public sealed class KatchDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KatchDbContext"/> class.
    /// </summary>
    /// <param name="options">Opções do contexto, com o fornecedor e os tipos enumerados.</param>
    public KatchDbContext(DbContextOptions<KatchDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets the accounts of the users (<c>app_user</c>).
    /// </summary>
    public DbSet<AppUser> Users => this.Set<AppUser>();

    /// <summary>
    /// Gets the recruiters (<c>recruiter</c>).
    /// </summary>
    public DbSet<Recruiter> Recruiters => this.Set<Recruiter>();

    /// <summary>
    /// Gets the candidates and their professional profiles (<c>candidate</c>).
    /// </summary>
    public DbSet<Candidate> Candidates => this.Set<Candidate>();

    /// <summary>
    /// Gets the pre-defined list of locations (<c>location</c>), read-only in the application.
    /// </summary>
    public DbSet<Location> Locations => this.Set<Location>();

    /// <summary>
    /// Gets the professional links of the candidates (<c>candidate_link</c>).
    /// </summary>
    public DbSet<CandidateLink> CandidateLinks => this.Set<CandidateLink>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        bool usesPostgres = this.Database.IsNpgsql();

        if (usesPostgres)
        {
            modelBuilder.HasKatchEnums();
        }

        modelBuilder.ApplyConfiguration(new AppUserConfiguration(usesPostgres));
        modelBuilder.ApplyConfiguration(new RecruiterConfiguration());
        modelBuilder.ApplyConfiguration(new LocationConfiguration(usesPostgres));
        modelBuilder.ApplyConfiguration(new CandidateConfiguration(usesPostgres));
        modelBuilder.ApplyConfiguration(new CandidateLinkConfiguration(usesPostgres));
    }
}
