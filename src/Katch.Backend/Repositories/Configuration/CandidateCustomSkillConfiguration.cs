// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="CandidateCustomSkill"/> para a tabela <c>candidate_custom_skill</c> (modelo de
/// dados v02, secções 5.2, 5.3, 6.8 e 7.1; DM-16). A chave estrangeira para <c>candidate</c>, com eliminação em cascata,
/// é configurada em <see cref="CandidateConfiguration"/>.
/// </summary>
/// <remarks>
/// O <c>CHECK</c> do P07 é escrito em SQL do PostgreSQL (expressão regular) e só é declarado quando o fornecedor é o
/// Npgsql (ver <see cref="AppUserConfiguration"/>).
/// </remarks>
public sealed class CandidateCustomSkillConfiguration : IEntityTypeConfiguration<CandidateCustomSkill>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateCustomSkillConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public CandidateCustomSkillConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CandidateCustomSkill> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("candidate_custom_skill", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint(
                    "ck_candidate_custom_skill_label",
                    "char_length(label) BETWEEN 2 AND 40 AND btrim(label) <> '' AND label ~ '^[[:alnum:] +#.-]+$'");
            }
        });

        builder.HasKey(customSkill => customSkill.Id);

        // O identificador é gerado pela entidade. Sem ValueGeneratedNever, uma competência nova acrescentada a um
        // Candidato já carregado seria tratada pelo EF Core como existente (chave preenchida) e daria um UPDATE sem linhas.
        builder.Property(customSkill => customSkill.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        builder.Property(customSkill => customSkill.Label)
            .HasMaxLength(CandidateCustomSkill.MaxLabelLength)
            .IsRequired();

        // O índice único sobre (candidate_id, lower(label)) (uq_candidate_custom_skill) é uma expressão, que o modelo do
        // EF Core não representa: é criado e removido por SQL na migration da I049. Cobre também a chave estrangeira.
    }
}
