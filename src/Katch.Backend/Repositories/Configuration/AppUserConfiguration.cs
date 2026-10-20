// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="AppUser"/> para a tabela <c>app_user</c> (modelo de dados v02, secções 5.2, 5.3 e 6.1).
/// </summary>
/// <remarks>
/// As restrições escritas em SQL do PostgreSQL (expressões regulares e comparações com os tipos enumerados) só são
/// declaradas quando o fornecedor é o Npgsql, para que os testes de integração sobre SQLite continuem a criar o esquema.
/// </remarks>
public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUserConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public AppUserConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("app_user", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint("ck_app_user_email", "email ~ '^[^@[:space:]]+@[^@[:space:]]+$'");
                table.HasCheckConstraint("ck_app_user_terms", "user_type = 'ADMIN' OR terms_accepted_at IS NOT NULL");
                table.HasCheckConstraint("ck_app_user_suspended", "status <> 'SUSPENDED' OR user_type = 'CANDIDATE'");
                table.HasCheckConstraint("ck_app_user_admin_active", "user_type <> 'ADMIN' OR status = 'ACTIVE'");
            }
        });

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(user => user.Email)
            .HasMaxLength(AppUser.MaxLength)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(AppUser.MaxLength)
            .IsRequired();

        builder.Property(user => user.UserType)
            .IsRequired();

        builder.Property(user => user.Status)
            .HasDefaultValue(AccountStatus.Active)
            .IsRequired();

        builder.Property(user => user.TermsAcceptedAt);

        builder.Property(user => user.CreatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .HasDefaultValueSql("now()")
            .IsRequired();

        // O índice único sobre lower(email) (uq_app_user_email) é uma expressão, que o modelo do EF Core não
        // representa: é criado e removido por SQL na migration AddAppUser.
    }
}
