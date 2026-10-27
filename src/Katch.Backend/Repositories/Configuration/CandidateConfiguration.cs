// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="Candidate"/> para a tabela <c>candidate</c> (modelo de dados v02, secções 5.2, 5.3,
/// 6.4 e 7.3). A chave primária é também chave estrangeira para <c>app_user</c>, na relação 1 : 0..1. Configura também as
/// competências do Candidato (secção 6.8, DM-16): a associação N:M <c>candidate_skill</c> à lista pré-definida e as
/// competências «Outro» de <c>candidate_custom_skill</c>.
/// </summary>
/// <remarks>
/// As restrições escritas em SQL do PostgreSQL só são declaradas quando o fornecedor é o Npgsql (ver
/// <see cref="AppUserConfiguration"/>). A marca de concorrência <c>xmin</c> também é específica do PostgreSQL.
/// </remarks>
public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public CandidateConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("candidate", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint("ck_candidate_full_name", "btrim(full_name) <> ''");
                table.HasCheckConstraint("ck_candidate_phone_number", "phone_number ~ '^[0-9]{9}$'");
                table.HasCheckConstraint("ck_candidate_max_distance_km", "max_distance_km BETWEEN 1 AND 500");
                table.HasCheckConstraint("ck_candidate_min_salary_expectation", "min_salary_expectation >= 0");
                table.HasCheckConstraint("ck_candidate_swipe_rights_remaining", "swipe_rights_remaining BETWEEN 0 AND 10");
                table.HasCheckConstraint("ck_candidate_quota_block", "next_swipe_available_at IS NULL OR swipe_rights_remaining = 0");
            }
        });

        builder.HasKey(candidate => candidate.Id);

        builder.Property(candidate => candidate.Id)
            .ValueGeneratedNever();

        builder.HasOne(candidate => candidate.User)
            .WithOne()
            .HasForeignKey<Candidate>(candidate => candidate.Id)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(candidate => candidate.LocationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(candidate => candidate.LocationId)
            .HasDatabaseName("ix_candidate_location");

        builder.Property(candidate => candidate.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(candidate => candidate.PhoneNumber)
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(candidate => candidate.DesiredRole)
            .HasMaxLength(150);

        builder.Property(candidate => candidate.ExperienceSummary)
            .HasMaxLength(1000);

        builder.Property(candidate => candidate.ProfilePhotoPath)
            .HasMaxLength(500);

        builder.Property(candidate => candidate.CvPath)
            .HasMaxLength(500);

        builder.Property(candidate => candidate.MinSalaryExpectation)
            .HasPrecision(10, 2);

        builder.Property(candidate => candidate.PreferredWorkModes)
            .HasDefaultValueSql("'{}'")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(candidate => candidate.PreferredContractTypes)
            .HasDefaultValueSql("'{}'")
            .ValueGeneratedNever()
            .IsRequired();

        // O valor 0 é válido (quota esgotada) e não pode ser confundido com «sem valor» e substituído pelo predefinido.
        builder.Property(candidate => candidate.SwipeRightsRemaining)
            .HasDefaultValue(Candidate.MaxInterests)
            .ValueGeneratedNever();

        if (this.usesPostgres)
        {
            // Marca de concorrência otimista: a coluna de sistema xmin, sem coluna própria (secção 7.3).
            builder.Property(candidate => candidate.Version)
                .IsRowVersion();
        }
        else
        {
            builder.Ignore(candidate => candidate.Version);

            // O SQLite guarda o DateTimeOffset como texto e o EF Core não traduz comparações sobre ele, que a consulta das
            // quotas a repor (ListQuotaDueAsync) usa. Guardado como ticks UTC, a ordem é a do instante, sem perder
            // precisão (o DateTimeOffsetToBinaryConverter perde-a abaixo do milissegundo). Só vale para os testes sobre
            // SQLite: no PostgreSQL a coluna é timestamptz, sem conversão.
            builder.Property(candidate => candidate.NextSwipeAvailableAt)
                .HasConversion(new ValueConverter<DateTimeOffset, long>(
                    instant => instant.UtcTicks,
                    ticks => new DateTimeOffset(ticks, TimeSpan.Zero)));
        }

        builder.HasMany(candidate => candidate.Links)
            .WithOne()
            .HasForeignKey(link => link.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(candidate => candidate.Links)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // candidate_custom_skill: competências «Outro», eliminadas com o Candidato.
        builder.HasMany(candidate => candidate.CustomSkills)
            .WithOne()
            .HasForeignKey(customSkill => customSkill.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(candidate => candidate.CustomSkills)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // candidate_skill: associação N:M pura à lista pré-definida, sem classe própria (DM-16, DC-09). A chave primária
        // (candidate_id, skill_id) impede a mesma competência duas vezes; a linha é eliminada com o Candidato, mas uma
        // competência associada não pode ser eliminada.
        builder.HasMany(candidate => candidate.Skills)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "candidate_skill",
                right => right.HasOne<Skill>().WithMany().HasForeignKey("SkillId").OnDelete(DeleteBehavior.NoAction),
                left => left.HasOne<Candidate>().WithMany().HasForeignKey("CandidateId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("candidate_skill");
                    join.HasKey("CandidateId", "SkillId");
                });

        builder.Navigation(candidate => candidate.Skills)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
