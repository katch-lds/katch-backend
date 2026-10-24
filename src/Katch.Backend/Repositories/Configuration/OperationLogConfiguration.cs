// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="OperationLog"/> para a tabela <c>operation_log</c> (modelo de dados v02, secções
/// 5.2, 5.3, 6.15 e 7.1): identificador <c>bigint</c> gerado sempre pela base de dados, chave estrangeira para o autor,
/// restrição <c>CHECK</c> do autor e índices por entidade e por operação.
/// </summary>
/// <remarks>
/// A restrição <c>CHECK</c> e a identidade <c>GENERATED ALWAYS</c> são específicas do PostgreSQL, por isso só são
/// declaradas quando o fornecedor é o Npgsql (ver <see cref="AppUserConfiguration"/>); no SQLite dos testes de
/// integração o identificador é uma chave autoincrementada.
/// </remarks>
public sealed class OperationLogConfiguration : IEntityTypeConfiguration<OperationLog>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationLogConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public OperationLogConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OperationLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("operation_log", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint("ck_operation_log_author", "user_id IS NOT NULL OR operation = 'JOB_CLOSED'");
            }
        });

        builder.HasKey(log => log.Id);

        // O identificador é gerado pela base de dados: GENERATED ALWAYS no PostgreSQL e chave autoincrementada
        // no SQLite dos testes.
        PropertyBuilder<long> id = builder.Property(log => log.Id)
            .ValueGeneratedOnAdd();

        if (this.usesPostgres)
        {
            id.UseIdentityAlwaysColumn();
        }

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(log => log.Operation)
            .IsRequired();

        builder.Property(log => log.OccurredAt)
            .HasDefaultValueSql("now()")
            .ValueGeneratedNever();

        // Histórico de uma entidade e consulta por operação e período.
        builder.HasIndex(log => new { log.EntityId, log.OccurredAt })
            .HasDatabaseName("ix_operation_log_entity");

        builder.HasIndex(log => new { log.Operation, log.OccurredAt })
            .HasDatabaseName("ix_operation_log_occurred");
    }
}
