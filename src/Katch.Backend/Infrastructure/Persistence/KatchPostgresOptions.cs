// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories.Configuration;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Katch.Backend.Infrastructure.Persistence;

/// <summary>
/// Configuração do <see cref="Repositories.KatchDbContext"/> sobre PostgreSQL 18: fornecedor Npgsql,
/// nomes em <c>snake_case</c> e mapeamento das enumerações para os tipos do modelo de dados (secção 5.1).
/// A declaração dos tipos no modelo fica em <see cref="KatchEnumConfiguration"/>, na camada Repositories.
/// </summary>
public static class KatchPostgresOptions
{
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

        INpgsqlNameTranslator translator = KatchEnumConfiguration.ValueTranslator;

        return npgsql
            .MapEnum<UserType>("user_type", nameTranslator: translator)
            .MapEnum<AccountStatus>("account_status", nameTranslator: translator)
            .MapEnum<WorkMode>("work_mode", nameTranslator: translator)
            .MapEnum<ContractType>("contract_type", nameTranslator: translator)
            .MapEnum<Availability>("availability", nameTranslator: translator)
            .MapEnum<Industry>("industry", nameTranslator: translator)
            .MapEnum<CompanyStatus>("company_status", nameTranslator: translator)
            .MapEnum<JobStatus>("job_status", nameTranslator: translator)
            .MapEnum<MatchStatus>("match_status", nameTranslator: translator)
            .MapEnum<ConversationStatus>("conversation_status", nameTranslator: translator)
            .MapEnum<ConversationCloseReason>("conversation_close_reason", nameTranslator: translator)
            .MapEnum<OperationType>("operation_type", nameTranslator: translator);
    }
}
