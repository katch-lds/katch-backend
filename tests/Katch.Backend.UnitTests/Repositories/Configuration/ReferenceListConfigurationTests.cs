// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Katch.Backend.UnitTests.Repositories.Configuration;

/// <summary>
/// Testes do mapeamento das listas pré-definidas e das associações contra o modelo de dados v02 (secções 5.2, 5.3, 6.7
/// a 6.9, 6.12 e 7.1): <c>skill</c>, <c>benefit</c>, <c>candidate_skill</c>, <c>candidate_custom_skill</c>,
/// <c>job_skill</c> e <c>job_benefit</c>. O modelo é verificado com o fornecedor Npgsql, sem abrir ligação.
/// </summary>
public sealed class ReferenceListConfigurationTests
{
    /// <summary>
    /// As tabelas <c>skill</c> e <c>benefit</c> têm só <c>id</c> e <c>name</c>, com <c>varchar(100)</c> obrigatório,
    /// identificador com <c>gen_random_uuid()</c> e o <c>CHECK</c> da designação não vazia.
    /// </summary>
    /// <param name="entityType">Entidade da lista pré-definida.</param>
    /// <param name="table">Tabela correspondente.</param>
    [Theory]
    [InlineData(typeof(Skill), "skill")]
    [InlineData(typeof(Benefit), "benefit")]
    public void MapsTheReferenceListTables(Type entityType, string table)
    {
        IEntityType entity = GetModel().FindEntityType(entityType)!;
        var store = StoreObjectIdentifier.Table(table);

        Assert.Equal(table, entity.GetTableName());
        Assert.Equal(["id", "name"], ColumnNames(entity, store));
        Assert.Equal("uuid", entity.FindProperty("Id")!.GetColumnType());
        Assert.Equal("gen_random_uuid()", entity.FindProperty("Id")!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty("Id")!.ValueGenerated);
        Assert.Equal("character varying(100)", entity.FindProperty("Name")!.GetColumnType());
        Assert.False(entity.FindProperty("Name")!.IsNullable);
        Assert.Equal("btrim(name) <> ''", Assert.Single(entity.GetCheckConstraints(), check => check.Name == $"ck_{table}_name").Sql);
        Assert.Single(entity.GetCheckConstraints());
    }

    /// <summary>
    /// A tabela <c>candidate_custom_skill</c> tem <c>id</c>, <c>candidate_id</c> e <c>label</c> (<c>varchar(40)</c>), o
    /// <c>CHECK</c> do P07 e a chave estrangeira para <c>candidate</c> com eliminação em cascata.
    /// </summary>
    [Fact]
    public void MapsTheCustomSkillTable()
    {
        IEntityType entity = GetModel().FindEntityType(typeof(CandidateCustomSkill))!;
        var store = StoreObjectIdentifier.Table("candidate_custom_skill");

        Assert.Equal("candidate_custom_skill", entity.GetTableName());
        Assert.Equal(["candidate_id", "id", "label"], ColumnNames(entity, store));
        Assert.Equal("character varying(40)", entity.FindProperty(nameof(CandidateCustomSkill.Label))!.GetColumnType());
        Assert.DoesNotContain(entity.GetProperties(), property => property.IsNullable);
        Assert.Equal("gen_random_uuid()", entity.FindProperty(nameof(CandidateCustomSkill.Id))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(CandidateCustomSkill.Id))!.ValueGenerated);
        Assert.Equal(
            "char_length(label) BETWEEN 2 AND 40 AND btrim(label) <> '' AND label ~ '^[[:alnum:] +#.-]+$'",
            Assert.Single(entity.GetCheckConstraints()).Sql);

        IForeignKey candidate = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(Candidate), candidate.PrincipalEntityType.ClrType);
        Assert.Equal("candidate_id", Assert.Single(candidate.Properties).GetColumnName(store));
        Assert.Equal(DeleteBehavior.Cascade, candidate.DeleteBehavior);
    }

    /// <summary>
    /// As associações N:M não têm classe própria: cada tabela tem só as duas colunas da chave primária composta, a
    /// chave estrangeira para o Candidato ou a vaga com eliminação em cascata e a chave estrangeira para a lista sem
    /// cascata (secções 5.2 e 6.12).
    /// </summary>
    /// <param name="table">Tabela de associação.</param>
    /// <param name="ownerColumn">Coluna da entidade que tem a associação.</param>
    /// <param name="ownerType">Entidade que tem a associação.</param>
    /// <param name="listColumn">Coluna da lista pré-definida.</param>
    /// <param name="listType">Entidade da lista pré-definida.</param>
    [Theory]
    [InlineData("candidate_skill", "candidate_id", typeof(Candidate), "skill_id", typeof(Skill))]
    [InlineData("job_skill", "job_id", typeof(Job), "skill_id", typeof(Skill))]
    [InlineData("job_benefit", "job_id", typeof(Job), "benefit_id", typeof(Benefit))]
    public void MapsTheAssociationTables(string table, string ownerColumn, Type ownerType, string listColumn, Type listType)
    {
        IEntityType entity = GetModel().FindEntityType(table)!;
        var store = StoreObjectIdentifier.Table(table);

        Assert.True(entity.HasSharedClrType);
        Assert.Equal(table, entity.GetTableName());
        string[] columns = [listColumn, ownerColumn];
        Array.Sort(columns, StringComparer.Ordinal);
        Assert.Equal(columns, ColumnNames(entity, store));
        Assert.Equal(
            [ownerColumn, listColumn],
            entity.FindPrimaryKey()!.Properties.Select(property => property.GetColumnName(store)!).ToArray());
        Assert.Equal($"pk_{table}", entity.FindPrimaryKey()!.GetName());

        IForeignKey owner = Assert.Single(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == ownerType);
        Assert.Equal(ownerColumn, Assert.Single(owner.Properties).GetColumnName(store));
        Assert.Equal(DeleteBehavior.Cascade, owner.DeleteBehavior);

        IForeignKey list = Assert.Single(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == listType);
        Assert.Equal(listColumn, Assert.Single(list.Properties).GetColumnName(store));
        Assert.Equal(DeleteBehavior.NoAction, list.DeleteBehavior);
    }

    /// <summary>
    /// As navegações seguem o modelo de classes: <c>Candidate.Skills</c>, <c>Job.Skills</c> e <c>Job.Benefits</c> são
    /// navegações N:M sobre as tabelas de associação, e <c>Candidate.CustomSkills</c> é uma navegação 1 : 0..*.
    /// </summary>
    [Fact]
    public void MapsTheNavigationsOfTheClassModel()
    {
        IModel model = GetModel();
        IEntityType candidate = model.FindEntityType(typeof(Candidate))!;
        IEntityType job = model.FindEntityType(typeof(Job))!;

        Assert.Equal("candidate_skill", candidate.FindSkipNavigation(nameof(Candidate.Skills))!.JoinEntityType.Name);
        Assert.Equal("job_skill", job.FindSkipNavigation(nameof(Job.Skills))!.JoinEntityType.Name);
        Assert.Equal("job_benefit", job.FindSkipNavigation(nameof(Job.Benefits))!.JoinEntityType.Name);
        Assert.Equal(
            typeof(CandidateCustomSkill),
            candidate.FindNavigation(nameof(Candidate.CustomSkills))!.TargetEntityType.ClrType);
    }

    /// <summary>
    /// Os índices únicos sobre <c>lower(name)</c> e sobre (<c>candidate_id</c>, <c>lower(label)</c>) são expressões, que
    /// o modelo do EF Core não representa: as seis tabelas não têm índices no modelo, nem os criados automaticamente
    /// para as chaves estrangeiras (secção 5.3).
    /// </summary>
    [Fact]
    public void TheTablesHaveNoIndexesInTheModel()
    {
        IModel model = GetModel();

        Assert.Empty(model.FindEntityType(typeof(Skill))!.GetIndexes());
        Assert.Empty(model.FindEntityType(typeof(Benefit))!.GetIndexes());
        Assert.Empty(model.FindEntityType(typeof(CandidateCustomSkill))!.GetIndexes());
        Assert.Empty(model.FindEntityType("candidate_skill")!.GetIndexes());
        Assert.Empty(model.FindEntityType("job_skill")!.GetIndexes());
        Assert.Empty(model.FindEntityType("job_benefit")!.GetIndexes());
    }

    private static string[] ColumnNames(IEntityType entity, StoreObjectIdentifier store)
    {
        return entity.GetProperties().Select(property => property.GetColumnName(store)!).Order(StringComparer.Ordinal).ToArray();
    }

    private static IModel GetModel()
    {
        var builder = new DbContextOptionsBuilder<KatchDbContext>();
        builder.UseKatchPostgres("Host=localhost;Database=katch");

        using var context = new KatchDbContext(builder.Options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
