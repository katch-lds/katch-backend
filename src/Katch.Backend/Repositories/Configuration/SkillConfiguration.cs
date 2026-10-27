// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="Skill"/> para a tabela <c>skill</c> (modelo de dados v02, secções 5.2, 5.3, 6.7 e
/// 7.1). As associações aos Candidatos e às vagas são configuradas em <see cref="CandidateConfiguration"/> e
/// <see cref="JobConfiguration"/>.
/// </summary>
/// <remarks>
/// O <c>CHECK</c> só é declarado quando o fornecedor é o Npgsql (ver <see cref="AppUserConfiguration"/>).
/// </remarks>
public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public SkillConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("skill", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint("ck_skill_name", "btrim(name) <> ''");
            }
        });

        builder.HasKey(skill => skill.Id);

        // O identificador é criado pela entidade; o predefinido gen_random_uuid() serve as inserções por SQL
        // (dados iniciais).
        builder.Property(skill => skill.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        builder.Property(skill => skill.Name)
            .HasMaxLength(Skill.MaxNameLength)
            .IsRequired();

        // O índice único sobre lower(name) (uq_skill_name) é uma expressão, que o modelo do EF Core não representa:
        // é criado e removido por SQL na migration da I049.
    }
}
