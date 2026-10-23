// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="Job"/> para a tabela <c>job</c> (modelo de dados v02, secções 5.2, 5.3, 6.11 e
/// 7.1), com o filtro global que exclui as vagas eliminadas de todas as consultas (secção 7.2, DM-09), e as associações
/// N:M <c>job_skill</c> e <c>job_benefit</c> às listas pré-definidas (secção 6.12).
/// </summary>
/// <remarks>
/// As restrições escritas em SQL do PostgreSQL (<c>cardinality()</c> e comparações com os tipos enumerados) só são
/// declaradas quando o fornecedor é o Npgsql (ver <see cref="AppUserConfiguration"/>).
/// </remarks>
public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public JobConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("job", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint("ck_job_title", "btrim(title) <> ''");
                table.HasCheckConstraint("ck_job_description", "btrim(description) <> ''");
                table.HasCheckConstraint("ck_job_min_salary", "min_salary > 0");
                table.HasCheckConstraint("ck_job_salary", "max_salary >= min_salary");
                table.HasCheckConstraint("ck_job_photo_paths", "cardinality(photo_paths) <= 5");
                table.HasCheckConstraint(
                    "ck_job_published",
                    "status NOT IN ('PUBLISHED', 'SUSPENDED') OR first_published_at IS NOT NULL");
            }
        });

        // As vagas eliminadas deixam de ser apresentadas e de poder ser geridas (RF058, RF114; DM-09).
        builder.HasQueryFilter(job => job.DeletedAt == null);

        builder.HasKey(job => job.Id);

        // O identificador é criado pela entidade; o predefinido gen_random_uuid() serve as inserções por SQL
        // (dados iniciais).
        builder.Property(job => job.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(job => job.CompanyId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(job => job.LocationId)
            .OnDelete(DeleteBehavior.NoAction);

        // Índices da secção 5.3: vagas por Empresa e estado e por localidade, só das não eliminadas, e vagas publicadas
        // por data-limite (encerramento automático, RF057). Os filtros são SQL do PostgreSQL, com os nomes das colunas
        // em snake_case e o tipo job_status, por isso só são declarados com o Npgsql; no SQLite dos testes de
        // integração os índices existem sem filtro.
        IndexBuilder<Job> companyStatus = builder.HasIndex(job => new { job.CompanyId, job.Status })
            .HasDatabaseName("ix_job_company_status");

        IndexBuilder<Job> location = builder.HasIndex(job => job.LocationId)
            .HasDatabaseName("ix_job_location");

        IndexBuilder<Job> expiry = builder.HasIndex(job => job.ExpiresAt)
            .HasDatabaseName("ix_job_expiry");

        if (this.usesPostgres)
        {
            companyStatus.HasFilter("deleted_at IS NULL");
            location.HasFilter("deleted_at IS NULL");
            expiry.HasFilter("status = 'PUBLISHED'");
        }

        builder.Property(job => job.Title)
            .HasMaxLength(Job.MaxTitleLength)
            .IsRequired();

        builder.Property(job => job.Description)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(job => job.MinSalary)
            .HasPrecision(10, 2);

        builder.Property(job => job.MaxSalary)
            .HasPrecision(10, 2);

        builder.Property(job => job.ContractType)
            .IsRequired();

        builder.Property(job => job.WorkMode)
            .IsRequired();

        // O valor false é válido e não pode ser confundido com «sem valor» e substituído pelo predefinido.
        builder.Property(job => job.IsUrgent)
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        // varchar(500)[]: cada caminho tem no máximo 500 caracteres; a lista vazia é o predefinido.
        builder.PrimitiveCollection(job => job.PhotoPaths)
            .ElementType(element => element.HasMaxLength(Job.MaxPathLength))
            .HasDefaultValueSql("'{}'")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(job => job.Status)
            .HasDefaultValue(JobStatus.Unpublished)
            .IsRequired();

        // job_skill e job_benefit: associações N:M puras, sem classe própria (modelo de classes, secção 9). As linhas são
        // eliminadas com a vaga; uma competência ou um benefício associado não pode ser eliminado.
        builder.HasMany(job => job.Skills)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "job_skill",
                right => right.HasOne<Skill>().WithMany().HasForeignKey("SkillId").OnDelete(DeleteBehavior.NoAction),
                left => left.HasOne<Job>().WithMany().HasForeignKey("JobId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("job_skill");
                    join.HasKey("JobId", "SkillId");
                });

        builder.HasMany(job => job.Benefits)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "job_benefit",
                right => right.HasOne<Benefit>().WithMany().HasForeignKey("BenefitId").OnDelete(DeleteBehavior.NoAction),
                left => left.HasOne<Job>().WithMany().HasForeignKey("JobId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("job_benefit");
                    join.HasKey("JobId", "BenefitId");
                });
    }
}
