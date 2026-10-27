// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Katch.Backend.UnitTests.Infrastructure.Persistence;

/// <summary>
/// Testes das operações da migration <see cref="AddSkillsAndBenefits"/>. Os índices únicos sobre <c>lower(name)</c> e
/// sobre (<c>candidate_id</c>, <c>lower(label)</c>) são criados por SQL e não constam do snapshot, por isso o EF Core não
/// os repõe se forem apagados da migration; estes testes fixam o <c>Up</c> e o <c>Down</c> sem precisar de uma base de
/// dados (modelo de dados v02, secções 5.2 e 5.3).
/// </summary>
public sealed class AddSkillsAndBenefitsMigrationTests
{
    /// <summary>
    /// O <c>Up</c> cria as seis tabelas e depois os três índices únicos sobre expressões.
    /// </summary>
    [Fact]
    public void UpCreatesTheSixTablesAndThenTheExpressionIndexes()
    {
        var migration = new AddSkillsAndBenefits();

        Assert.Equal(9, migration.UpOperations.Count);
        Assert.Equal(
            ["benefit", "candidate_custom_skill", "skill", "job_benefit", "candidate_skill", "job_skill"],
            migration.UpOperations.Take(6).Select(operation => Assert.IsType<CreateTableOperation>(operation).Name).ToArray());
        Assert.Equal(
            [
                "CREATE UNIQUE INDEX uq_skill_name ON skill (lower(name));",
                "CREATE UNIQUE INDEX uq_benefit_name ON benefit (lower(name));",
                "CREATE UNIQUE INDEX uq_candidate_custom_skill ON candidate_custom_skill (candidate_id, lower(label));",
            ],
            migration.UpOperations.Skip(6).Select(operation => Assert.IsType<SqlOperation>(operation).Sql).ToArray());
    }

    /// <summary>
    /// As tabelas de associação têm a chave primária composta e as duas chaves estrangeiras, com eliminação em cascata
    /// só do lado do Candidato ou da vaga.
    /// </summary>
    /// <param name="table">Tabela de associação.</param>
    /// <param name="ownerColumn">Coluna do Candidato ou da vaga.</param>
    /// <param name="listColumn">Coluna da lista pré-definida.</param>
    [Theory]
    [InlineData("candidate_skill", "candidate_id", "skill_id")]
    [InlineData("job_skill", "job_id", "skill_id")]
    [InlineData("job_benefit", "job_id", "benefit_id")]
    public void AssociationTablesHaveCompositeKeyAndCascadeOnlyOnTheOwner(string table, string ownerColumn, string listColumn)
    {
        CreateTableOperation operation = new AddSkillsAndBenefits().UpOperations
            .OfType<CreateTableOperation>()
            .Single(item => item.Name == table);

        Assert.Equal([ownerColumn, listColumn], operation.PrimaryKey!.Columns);
        Assert.Equal(
            ReferentialAction.Cascade,
            operation.ForeignKeys.Single(key => key.Columns.Single() == ownerColumn).OnDelete);
        Assert.Equal(
            ReferentialAction.NoAction,
            operation.ForeignKeys.Single(key => key.Columns.Single() == listColumn).OnDelete);
    }

    /// <summary>
    /// O <c>Down</c> apaga primeiro os índices e depois as seis tabelas.
    /// </summary>
    [Fact]
    public void DownDropsTheIndexesAndThenTheTables()
    {
        var migration = new AddSkillsAndBenefits();

        Assert.Equal(9, migration.DownOperations.Count);
        Assert.Equal(
            ["DROP INDEX uq_candidate_custom_skill;", "DROP INDEX uq_benefit_name;", "DROP INDEX uq_skill_name;"],
            migration.DownOperations.Take(3).Select(operation => Assert.IsType<SqlOperation>(operation).Sql).ToArray());
        Assert.Equal(
            ["candidate_custom_skill", "candidate_skill", "job_benefit", "job_skill", "benefit", "skill"],
            migration.DownOperations.Skip(3).Select(operation => Assert.IsType<DropTableOperation>(operation).Name).ToArray());
    }
}
