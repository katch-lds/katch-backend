// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Katch.Backend.Infrastructure.Persistence;

/// <summary>
/// Configuração do <see cref="Repositories.KatchDbContext"/> sobre PostgreSQL 18: fornecedor Npgsql,
/// nomes em <c>snake_case</c> e tipos enumerados do modelo de dados (secção 5.1).
/// </summary>
public static class KatchPostgresOptions
{
    private static readonly INpgsqlNameTranslator EnumValueTranslator = new UpperSnakeCaseNameTranslator();

    /// <summary>
    /// Configura o contexto para a base de dados <c>katch</c> em PostgreSQL.
    /// </summary>
    /// <param name="options">Construtor das opções do contexto.</param>
    /// <param name="connectionString">Cadeia de ligação ao PostgreSQL.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static DbContextOptionsBuilder UseKatchPostgres(this DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(connectionString, npgsql => npgsql.MapKatchEnums())
            .UseSnakeCaseNamingConvention();
    }

    /// <summary>
    /// Associa cada enumeração do domínio ao tipo enumerado do PostgreSQL com o mesmo nome em <c>snake_case</c>.
    /// </summary>
    /// <param name="npgsql">Construtor das opções do Npgsql.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static NpgsqlDbContextOptionsBuilder MapKatchEnums(this NpgsqlDbContextOptionsBuilder npgsql)
    {
        ArgumentNullException.ThrowIfNull(npgsql);

        return npgsql
            .MapEnum<UserType>("user_type", nameTranslator: EnumValueTranslator)
            .MapEnum<AccountStatus>("account_status", nameTranslator: EnumValueTranslator)
            .MapEnum<WorkMode>("work_mode", nameTranslator: EnumValueTranslator)
            .MapEnum<ContractType>("contract_type", nameTranslator: EnumValueTranslator)
            .MapEnum<Availability>("availability", nameTranslator: EnumValueTranslator)
            .MapEnum<Industry>("industry", nameTranslator: EnumValueTranslator)
            .MapEnum<CompanyStatus>("company_status", nameTranslator: EnumValueTranslator)
            .MapEnum<JobStatus>("job_status", nameTranslator: EnumValueTranslator)
            .MapEnum<MatchStatus>("match_status", nameTranslator: EnumValueTranslator)
            .MapEnum<ConversationStatus>("conversation_status", nameTranslator: EnumValueTranslator)
            .MapEnum<ConversationCloseReason>("conversation_close_reason", nameTranslator: EnumValueTranslator)
            .MapEnum<OperationType>("operation_type", nameTranslator: EnumValueTranslator);
    }

    /// <summary>
    /// Declara no modelo os tipos enumerados do PostgreSQL, com os valores pela ordem do modelo de dados (secção 5.1).
    /// </summary>
    /// <param name="modelBuilder">Construtor do modelo do contexto.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static ModelBuilder HasKatchEnums(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        return modelBuilder
            .HasPostgresEnum<UserType>(name: "user_type", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<AccountStatus>(name: "account_status", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<WorkMode>(name: "work_mode", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<ContractType>(name: "contract_type", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<Availability>(name: "availability", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<Industry>(name: "industry", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<CompanyStatus>(name: "company_status", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<JobStatus>(name: "job_status", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<MatchStatus>(name: "match_status", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<ConversationStatus>(name: "conversation_status", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<ConversationCloseReason>(name: "conversation_close_reason", nameTranslator: EnumValueTranslator)
            .HasPostgresEnum<OperationType>(name: "operation_type", nameTranslator: EnumValueTranslator);
    }
}
