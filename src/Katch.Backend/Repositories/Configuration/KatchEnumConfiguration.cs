// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Declaração no modelo do <see cref="KatchDbContext"/> dos tipos enumerados do PostgreSQL
/// (modelo de dados, secção 5.1).
/// </summary>
public static class KatchEnumConfiguration
{
    /// <summary>
    /// Gets the translator of the enumeration members to the values of the data model, shared by the model
    /// and by the Npgsql mapping, so that both use exactly the same values.
    /// </summary>
    public static INpgsqlNameTranslator ValueTranslator { get; } = new UpperSnakeCaseNameTranslator();

    /// <summary>
    /// Declara no modelo os tipos enumerados do PostgreSQL, com os valores pela ordem do modelo de dados (secção 5.1).
    /// </summary>
    /// <param name="modelBuilder">Construtor do modelo do contexto.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static ModelBuilder HasKatchEnums(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        return modelBuilder
            .HasPostgresEnum<UserType>(name: "user_type", nameTranslator: ValueTranslator)
            .HasPostgresEnum<AccountStatus>(name: "account_status", nameTranslator: ValueTranslator)
            .HasPostgresEnum<WorkMode>(name: "work_mode", nameTranslator: ValueTranslator)
            .HasPostgresEnum<ContractType>(name: "contract_type", nameTranslator: ValueTranslator)
            .HasPostgresEnum<Availability>(name: "availability", nameTranslator: ValueTranslator)
            .HasPostgresEnum<Industry>(name: "industry", nameTranslator: ValueTranslator)
            .HasPostgresEnum<CompanyStatus>(name: "company_status", nameTranslator: ValueTranslator)
            .HasPostgresEnum<JobStatus>(name: "job_status", nameTranslator: ValueTranslator)
            .HasPostgresEnum<MatchStatus>(name: "match_status", nameTranslator: ValueTranslator)
            .HasPostgresEnum<ConversationStatus>(name: "conversation_status", nameTranslator: ValueTranslator)
            .HasPostgresEnum<ConversationCloseReason>(name: "conversation_close_reason", nameTranslator: ValueTranslator)
            .HasPostgresEnum<OperationType>(name: "operation_type", nameTranslator: ValueTranslator);
    }
}
