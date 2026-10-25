// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Katch.Backend.UnitTests.Infrastructure.Persistence;

/// <summary>
/// Testes das operações da migration <see cref="AddMatch"/>: a tabela <c>match</c> com as restrições <c>CHECK</c>, as
/// chaves estrangeiras, o <c>xmin</c> e os índices do modelo de dados v02 (secções 5.2, 5.3, 7.1 e 7.3), e a reversão no
/// <c>Down</c>, sem precisar de uma base de dados.
/// </summary>
public sealed class AddMatchMigrationTests
{
    /// <summary>
    /// O <c>Up</c> cria a tabela <c>match</c> com as 12 colunas do modelo de dados e o <c>xmin</c>, os cinco
    /// <c>CHECK</c> e as quatro chaves estrangeiras.
    /// </summary>
    [Fact]
    public void UpCreatesTheMatchTableWithChecksAndForeignKeys()
    {
        var migration = new AddMatch();

        CreateTableOperation table = Assert.IsType<CreateTableOperation>(migration.UpOperations[0]);

        Assert.Equal("match", table.Name);
        Assert.Equal(13, table.Columns.Count);
        Assert.Equal("pk_match", table.PrimaryKey!.Name);
        Assert.Equal(
            ["ck_match_closed_by", "ck_match_conversation", "ck_match_conversation_close", "ck_match_decision", "ck_match_declined"],
            table.CheckConstraints.Select(check => check.Name).ToArray());
        Assert.Equal(
            ["app_user", "candidate", "job", "recruiter"],
            table.ForeignKeys.Select(foreignKey => foreignKey.PrincipalTable).ToArray());
        Assert.Equal(
            ["fk_match_app_user_conversation_closed_by", "fk_match_candidate_candidate_id", "fk_match_job_job_id", "fk_match_recruiter_decided_by"],
            table.ForeignKeys.Select(foreignKey => foreignKey.Name).ToArray());
    }

    /// <summary>
    /// O <c>xmin</c> é a coluna de sistema <c>xid</c> usada como marca de concorrência, e só as colunas da abertura do
    /// perfil, da decisão e da conversa admitem <c>NULL</c>.
    /// </summary>
    [Fact]
    public void UpMapsTheXminColumnAndTheNullability()
    {
        var migration = new AddMatch();
        CreateTableOperation table = Assert.IsType<CreateTableOperation>(migration.UpOperations[0]);

        AddColumnOperation xmin = table.Columns.Single(column => column.Name == "xmin");
        Assert.Equal("xid", xmin.ColumnType);
        Assert.True(xmin.IsRowVersion);

        Assert.Equal(
            [
                "conversation_close_reason", "conversation_closed_at", "conversation_closed_by", "conversation_status",
                "decided_by", "profile_opened_at", "recruiter_action_at",
            ],
            table.Columns.Where(column => column.IsNullable).Select(column => column.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("now()", table.Columns.Single(column => column.Name == "candidate_action_at").DefaultValueSql);
        Assert.Equal("gen_random_uuid()", table.Columns.Single(column => column.Name == "id").DefaultValueSql);
    }

    /// <summary>
    /// O <c>Up</c> cria os três índices da secção 5.3 (um deles filtrado) e a unicidade Candidato–vaga.
    /// </summary>
    [Fact]
    public void UpCreatesTheIndexesAndTheUniqueness()
    {
        var migration = new AddMatch();

        CreateIndexOperation[] indexes = [.. migration.UpOperations.OfType<CreateIndexOperation>()];

        Assert.Equal(5, migration.UpOperations.Count);
        Assert.Equal(
            ["ix_match_interest_at", "ix_match_job_status", "ix_match_matched", "uq_match_candidate_job"],
            indexes.Select(index => index.Name).ToArray());
        Assert.Equal(["candidate_action_at"], indexes[0].Columns);
        Assert.Equal("status <> 'DECLINED_BY_CANDIDATE'", indexes[0].Filter);
        Assert.Equal(["job_id", "status"], indexes[1].Columns);
        Assert.Equal(["status", "recruiter_action_at"], indexes[2].Columns);
        Assert.Equal(["candidate_id", "job_id"], indexes[3].Columns);
        Assert.True(indexes[3].IsUnique);
        Assert.All(indexes.Take(3), index => Assert.False(index.IsUnique));
    }

    /// <summary>
    /// O <c>Down</c> apaga a tabela <c>match</c>, com as restrições e os índices.
    /// </summary>
    [Fact]
    public void DownDropsTheMatchTable()
    {
        var migration = new AddMatch();

        DropTableOperation table = Assert.IsType<DropTableOperation>(Assert.Single(migration.DownOperations));

        Assert.Equal("match", table.Name);
    }
}
