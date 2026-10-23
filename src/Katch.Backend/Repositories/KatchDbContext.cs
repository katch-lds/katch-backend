// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Repositories.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

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
    /// Gets the administrators (<c>admin</c>).
    /// </summary>
    public DbSet<Admin> Admins => this.Set<Admin>();

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

    /// <summary>
    /// Gets the skills of the pre-defined list (<c>skill</c>).
    /// </summary>
    public DbSet<Skill> Skills => this.Set<Skill>();

    /// <summary>
    /// Gets the benefits of the pre-defined list (<c>benefit</c>).
    /// </summary>
    public DbSet<Benefit> Benefits => this.Set<Benefit>();

    /// <summary>
    /// Gets the skills of the candidates from the «Other» option (<c>candidate_custom_skill</c>).
    /// </summary>
    public DbSet<CandidateCustomSkill> CandidateCustomSkills => this.Set<CandidateCustomSkill>();

    /// <summary>
    /// Gets the companies (<c>company</c>).
    /// </summary>
    public DbSet<Company> Companies => this.Set<Company>();

    /// <summary>
    /// Gets the jobs (<c>job</c>), without the deleted ones (global query filter on <c>deleted_at</c>).
    /// </summary>
    public DbSet<Job> Jobs => this.Set<Job>();

    /// <inheritdoc/>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        base.ConfigureConventions(configurationBuilder);

        // Os índices são os da secção 5.3 do modelo de dados, declarados em cada configuração. Sem esta linha, o EF Core
        // criava um índice por cada chave estrangeira não coberta por outro índice (por exemplo, company.location_id),
        // que o modelo de dados não prevê.
        configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();
    }

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
        modelBuilder.ApplyConfiguration(new AdminConfiguration());
        modelBuilder.ApplyConfiguration(new RecruiterConfiguration());
        modelBuilder.ApplyConfiguration(new LocationConfiguration(usesPostgres));
        modelBuilder.ApplyConfiguration(new SkillConfiguration(usesPostgres));
        modelBuilder.ApplyConfiguration(new BenefitConfiguration(usesPostgres));
        modelBuilder.ApplyConfiguration(new CandidateConfiguration(usesPostgres));
        modelBuilder.ApplyConfiguration(new CandidateLinkConfiguration(usesPostgres));
        modelBuilder.ApplyConfiguration(new CandidateCustomSkillConfiguration(usesPostgres));
        modelBuilder.ApplyConfiguration(new CompanyConfiguration(usesPostgres));
        modelBuilder.ApplyConfiguration(new JobConfiguration(usesPostgres));
    }
}
