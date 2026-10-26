// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="Benefit"/> para a tabela <c>benefit</c> (modelo de dados v02, secções 5.2, 5.3,
/// 6.9 e 7.1). A associação às vagas é configurada em <see cref="JobConfiguration"/>.
/// </summary>
/// <remarks>
/// O <c>CHECK</c> só é declarado quando o fornecedor é o Npgsql (ver <see cref="AppUserConfiguration"/>).
/// </remarks>
public sealed class BenefitConfiguration : IEntityTypeConfiguration<Benefit>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="BenefitConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public BenefitConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Benefit> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("benefit", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint("ck_benefit_name", "btrim(name) <> ''");
            }
        });

        builder.HasKey(benefit => benefit.Id);

        // O identificador é criado pela entidade; o predefinido gen_random_uuid() serve as inserções por SQL
        // (dados iniciais).
        builder.Property(benefit => benefit.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        builder.Property(benefit => benefit.Name)
            .HasMaxLength(Benefit.MaxNameLength)
            .IsRequired();

        // O índice único sobre lower(name) (uq_benefit_name) é uma expressão, que o modelo do EF Core não representa:
        // é criado e removido por SQL na migration da I049.
    }
}
