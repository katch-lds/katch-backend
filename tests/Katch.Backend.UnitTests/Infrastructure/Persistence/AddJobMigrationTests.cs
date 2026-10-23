// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Katch.Backend.UnitTests.Infrastructure.Persistence;

/// <summary>
/// Testes das operações da migration <see cref="AddJob"/>: a tabela <c>job</c> com as restrições <c>CHECK</c>, as chaves
/// estrangeiras e os índices do modelo de dados v02 (secções 5.2, 5.3 e 7.1), e a reversão no <c>Down</c>, sem precisar
/// de uma base de dados.
/// </summary>
public sealed class AddJobMigrationTests
{
    /// <summary>
    /// O <c>Up</c> cria a tabela <c>job</c> com as 17 colunas, os seis <c>CHECK</c> e as chaves estrangeiras para
    /// <c>company</c> e <c>location</c>.
    /// </summary>
    [Fact]
    public void UpCreatesTheJobTableWithChecksAndForeignKeys()
    {
        var migration = new AddJob();

        CreateTableOperation table = Assert.IsType<CreateTableOperation>(migration.UpOperations[0]);

        Assert.Equal("job", table.Name);
        Assert.Equal(17, table.Columns.Count);
        Assert.Equal("pk_job", table.PrimaryKey!.Name);
        Assert.Equal(
            ["ck_job_description", "ck_job_min_salary", "ck_job_photo_paths", "ck_job_published", "ck_job_salary", "ck_job_title"],
            table.CheckConstraints.Select(check => check.Name).ToArray());
        Assert.Equal(
            ["company", "location"],
            table.ForeignKeys.Select(foreignKey => foreignKey.PrincipalTable).ToArray());
        Assert.Equal(JobStatus.Unpublished, table.Columns.Single(column => column.Name == "status").DefaultValue);
        Assert.False(Assert.IsType<bool>(table.Columns.Single(column => column.Name == "is_urgent").DefaultValue));
    }

    /// <summary>
    /// O <c>Up</c> cria os três índices filtrados da secção 5.3.
    /// </summary>
    [Fact]
    public void UpCreatesTheFilteredIndexes()
    {
        var migration = new AddJob();

        CreateIndexOperation[] indexes = [.. migration.UpOperations.OfType<CreateIndexOperation>()];

        Assert.Equal(4, migration.UpOperations.Count);
        Assert.Equal(["ix_job_company_status", "ix_job_expiry", "ix_job_location"], indexes.Select(index => index.Name).ToArray());
        Assert.Equal(["company_id", "status"], indexes[0].Columns);
        Assert.Equal("deleted_at IS NULL", indexes[0].Filter);
        Assert.Equal(["expires_at"], indexes[1].Columns);
        Assert.Equal("status = 'PUBLISHED'", indexes[1].Filter);
        Assert.Equal(["location_id"], indexes[2].Columns);
        Assert.Equal("deleted_at IS NULL", indexes[2].Filter);
    }

    /// <summary>
    /// O <c>Down</c> apaga a tabela <c>job</c>, com as restrições e os índices.
    /// </summary>
    [Fact]
    public void DownDropsTheJobTable()
    {
        var migration = new AddJob();

        DropTableOperation table = Assert.IsType<DropTableOperation>(Assert.Single(migration.DownOperations));

        Assert.Equal("job", table.Name);
    }
}
