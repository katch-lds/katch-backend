// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="CandidateLink"/> para a tabela <c>candidate_link</c> (modelo de dados v02,
/// secções 5.2 e 6.6). A chave estrangeira para <c>candidate</c>, com eliminação em cascata, é declarada em
/// <see cref="CandidateConfiguration"/>.
/// </summary>
/// <remarks>
/// As restrições escritas em SQL do PostgreSQL só são declaradas quando o fornecedor é o Npgsql (ver
/// <see cref="AppUserConfiguration"/>).
/// </remarks>
public sealed class CandidateLinkConfiguration : IEntityTypeConfiguration<CandidateLink>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateLinkConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public CandidateLinkConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CandidateLink> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("candidate_link", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint("ck_candidate_link_position", "position BETWEEN 1 AND 3");
                table.HasCheckConstraint("ck_candidate_link_url", "url ~* '^https?://'");
            }
        });

        builder.HasKey(link => link.Id);

        builder.Property(link => link.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(link => link.Url)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(link => new { link.CandidateId, link.Position })
            .IsUnique()
            .HasDatabaseName("uq_candidate_link_position");

        builder.HasIndex(link => new { link.CandidateId, link.Url })
            .IsUnique()
            .HasDatabaseName("uq_candidate_link_url");
    }
}
