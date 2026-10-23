// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="Company"/> para a tabela <c>company</c> (modelo de dados v02, secções 5.2, 5.3,
/// 6.10 e 7.1). Cada Recrutador representa no máximo uma Empresa e cada Empresa tem exatamente um Recrutador
/// (<c>recruiter_id</c> obrigatório e único, relação 1 : 0..1, DM-03).
/// </summary>
/// <remarks>
/// As restrições escritas em SQL do PostgreSQL (expressões regulares, <c>cardinality()</c> e comparações com os tipos
/// enumerados) só são declaradas quando o fornecedor é o Npgsql (ver <see cref="AppUserConfiguration"/>).
/// </remarks>
public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public CompanyConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("company", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint("ck_company_company_name", "btrim(company_name) <> ''");
                table.HasCheckConstraint("ck_company_tax_id", "tax_id ~ '^[0-9]{9}$'");
                table.HasCheckConstraint("ck_company_address", "btrim(address) <> ''");
                table.HasCheckConstraint("ck_company_contact_email", "contact_email ~ '^[^@[:space:]]+@[^@[:space:]]+$'");
                table.HasCheckConstraint("ck_company_contact_phone", "contact_phone ~ '^[0-9]{9}$'");
                table.HasCheckConstraint("ck_company_responsible_name", "btrim(responsible_name) <> ''");
                table.HasCheckConstraint("ck_company_website", "website ~* '^https?://'");
                table.HasCheckConstraint("ck_company_photo_paths", "cardinality(photo_paths) <= 6");
                table.HasCheckConstraint("ck_company_reason", "(status = 'REJECTED') = (status_reason IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_company_reason_length",
                    "status_reason IS NULL OR char_length(btrim(status_reason)) BETWEEN 10 AND 500");
            }
        });

        builder.HasKey(company => company.Id);

        // O identificador é criado pela entidade; o predefinido gen_random_uuid() serve as inserções por SQL
        // (dados iniciais).
        builder.Property(company => company.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        builder.HasOne<Recruiter>()
            .WithOne()
            .HasForeignKey<Company>(company => company.RecruiterId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(company => company.RecruiterId)
            .IsUnique()
            .HasDatabaseName("uq_company_recruiter");

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(company => company.LocationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(company => company.TaxId)
            .IsUnique()
            .HasDatabaseName("uq_company_tax_id");

        builder.HasIndex(company => company.Status)
            .HasDatabaseName("ix_company_status");

        builder.Property(company => company.CompanyName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(company => company.TaxId)
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(company => company.Industry)
            .IsRequired();

        builder.Property(company => company.Address)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(company => company.ContactEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(company => company.ContactPhone)
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(company => company.ResponsibleName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(company => company.Website)
            .HasMaxLength(Company.MaxPathLength);

        builder.Property(company => company.Description)
            .HasMaxLength(Company.MaxDescriptionLength);

        builder.Property(company => company.LogoPath)
            .HasMaxLength(Company.MaxPathLength);

        // varchar(500)[]: cada caminho da galeria tem no máximo 500 caracteres; a lista vazia é o predefinido.
        builder.PrimitiveCollection(company => company.PhotoPaths)
            .ElementType(element => element.HasMaxLength(Company.MaxPathLength))
            .HasDefaultValueSql("'{}'")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(company => company.Status)
            .HasDefaultValue(CompanyStatus.Pending)
            .IsRequired();

        builder.Property(company => company.StatusReason)
            .HasMaxLength(Company.MaxReasonLength);

        builder.Property(company => company.SubmittedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();
    }
}
