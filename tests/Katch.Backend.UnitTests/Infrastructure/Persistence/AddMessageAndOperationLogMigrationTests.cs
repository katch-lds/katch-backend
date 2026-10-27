// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Katch.Backend.UnitTests.Infrastructure.Persistence;

/// <summary>
/// Testes das operações da migration <see cref="AddMessageAndOperationLog"/>: as tabelas <c>message</c> e
/// <c>operation_log</c> com as restrições <c>CHECK</c>, as chaves estrangeiras e os quatro índices do modelo de dados
/// v02 (secções 5.2, 5.3 e 7.1), e a reversão no <c>Down</c>, sem precisar de uma base de dados.
/// </summary>
public sealed class AddMessageAndOperationLogMigrationTests
{
    /// <summary>
    /// O <c>Up</c> só cria as duas tabelas e os quatro índices: não altera a base de dados nem recria os tipos
    /// enumerados, já criados na <c>InitialCreate</c>.
    /// </summary>
    [Fact]
    public void UpOnlyCreatesTheTwoTablesAndTheFourIndexes()
    {
        var migration = new AddMessageAndOperationLog();

        Assert.Equal(6, migration.UpOperations.Count);
        Assert.Equal(
            ["message", "operation_log"],
            migration.UpOperations.OfType<CreateTableOperation>().Select(table => table.Name).ToArray());
        Assert.Equal(4, migration.UpOperations.OfType<CreateIndexOperation>().Count());
        Assert.Empty(migration.UpOperations.OfType<AlterDatabaseOperation>());
    }

    /// <summary>
    /// O <c>Up</c> cria a tabela <c>message</c> com as seis colunas do modelo de dados, os dois <c>CHECK</c> e as duas
    /// chaves estrangeiras.
    /// </summary>
    [Fact]
    public void UpCreatesTheMessageTableWithChecksAndForeignKeys()
    {
        var migration = new AddMessageAndOperationLog();

        CreateTableOperation table = Assert.IsType<CreateTableOperation>(migration.UpOperations[0]);

        Assert.Equal("message", table.Name);
        Assert.Equal("pk_message", table.PrimaryKey!.Name);
        Assert.Equal(
            ["content", "id", "match_id", "read_at", "sender_id", "sent_at"],
            table.Columns.Select(column => column.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["ck_message_content", "ck_message_read"], table.CheckConstraints.Select(check => check.Name).ToArray());
        Assert.Equal("btrim(content) <> ''", table.CheckConstraints[0].Sql);
        Assert.Equal("read_at IS NULL OR read_at >= sent_at", table.CheckConstraints[1].Sql);
        Assert.Equal(
            ["fk_message_app_user_sender_id", "fk_message_match_match_id"],
            table.ForeignKeys.Select(foreignKey => foreignKey.Name).ToArray());
        Assert.Equal(["app_user", "match"], table.ForeignKeys.Select(foreignKey => foreignKey.PrincipalTable).ToArray());
    }

    /// <summary>
    /// Tipos, nulidade e predefinidos das colunas de <c>message</c>: o texto é <c>varchar(1000)</c> e só a data de
    /// leitura admite <c>NULL</c>.
    /// </summary>
    [Fact]
    public void UpMapsTheMessageColumns()
    {
        var migration = new AddMessageAndOperationLog();
        CreateTableOperation table = Assert.IsType<CreateTableOperation>(migration.UpOperations[0]);

        AddColumnOperation content = table.Columns.Single(column => column.Name == "content");
        Assert.Equal("character varying(1000)", content.ColumnType);
        Assert.Equal(1000, content.MaxLength);
        Assert.Equal(["read_at"], table.Columns.Where(column => column.IsNullable).Select(column => column.Name).ToArray());
        Assert.Equal("gen_random_uuid()", table.Columns.Single(column => column.Name == "id").DefaultValueSql);
        Assert.Equal("now()", table.Columns.Single(column => column.Name == "sent_at").DefaultValueSql);
        Assert.Equal("timestamp with time zone", table.Columns.Single(column => column.Name == "read_at").ColumnType);
    }

    /// <summary>
    /// O <c>Up</c> cria a tabela <c>operation_log</c> com as cinco colunas do modelo de dados, o identificador
    /// <c>bigint</c> com identidade <c>GENERATED ALWAYS</c>, o <c>CHECK</c> do autor e a chave estrangeira para
    /// <c>app_user</c>.
    /// </summary>
    [Fact]
    public void UpCreatesTheOperationLogTableWithIdentityCheckAndForeignKey()
    {
        var migration = new AddMessageAndOperationLog();

        CreateTableOperation table = Assert.IsType<CreateTableOperation>(migration.UpOperations[1]);

        Assert.Equal("operation_log", table.Name);
        Assert.Equal("pk_operation_log", table.PrimaryKey!.Name);
        Assert.Equal(
            ["entity_id", "id", "occurred_at", "operation", "user_id"],
            table.Columns.Select(column => column.Name).Order(StringComparer.Ordinal).ToArray());

        AddColumnOperation id = table.Columns.Single(column => column.Name == "id");
        Assert.Equal("bigint", id.ColumnType);
        Assert.Equal("IdentityAlwaysColumn", id["Npgsql:ValueGenerationStrategy"]!.ToString());
        Assert.Equal("operation_type", table.Columns.Single(column => column.Name == "operation").ColumnType);
        Assert.Equal(["user_id"], table.Columns.Where(column => column.IsNullable).Select(column => column.Name).ToArray());
        Assert.Equal("now()", table.Columns.Single(column => column.Name == "occurred_at").DefaultValueSql);

        AddCheckConstraintOperation check = Assert.Single(table.CheckConstraints);
        Assert.Equal("ck_operation_log_author", check.Name);
        Assert.Equal("user_id IS NOT NULL OR operation = 'JOB_CLOSED'", check.Sql);

        AddForeignKeyOperation foreignKey = Assert.Single(table.ForeignKeys);
        Assert.Equal("fk_operation_log_app_user_user_id", foreignKey.Name);
        Assert.Equal("app_user", foreignKey.PrincipalTable);
    }

    /// <summary>
    /// O <c>Up</c> cria os quatro índices da secção 5.3, só o das mensagens por ler com filtro.
    /// </summary>
    [Fact]
    public void UpCreatesTheIndexes()
    {
        var migration = new AddMessageAndOperationLog();

        CreateIndexOperation[] indexes = [.. migration.UpOperations.OfType<CreateIndexOperation>()];

        Assert.Equal(
            ["ix_message_match_sent", "ix_message_unread", "ix_operation_log_entity", "ix_operation_log_occurred"],
            indexes.Select(index => index.Name).ToArray());
        Assert.Equal(["match_id", "sent_at"], indexes[0].Columns);
        Assert.Null(indexes[0].Filter);
        Assert.Equal(["match_id"], indexes[1].Columns);
        Assert.Equal("read_at IS NULL", indexes[1].Filter);
        Assert.Equal(["entity_id", "occurred_at"], indexes[2].Columns);
        Assert.Null(indexes[2].Filter);
        Assert.Equal(["operation", "occurred_at"], indexes[3].Columns);
        Assert.Null(indexes[3].Filter);
        Assert.All(indexes, index => Assert.False(index.IsUnique));
    }

    /// <summary>
    /// O <c>Down</c> apaga as duas tabelas, <c>message</c> primeiro, e nada mais.
    /// </summary>
    [Fact]
    public void DownDropsTheTwoTables()
    {
        var migration = new AddMessageAndOperationLog();

        Assert.Equal(
            ["message", "operation_log"],
            migration.DownOperations.Select(operation => Assert.IsType<DropTableOperation>(operation).Name).ToArray());
    }
}
