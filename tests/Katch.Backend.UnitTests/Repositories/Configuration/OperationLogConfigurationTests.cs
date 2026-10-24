// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Katch.Backend.UnitTests.Repositories.Configuration;

/// <summary>
/// Testes do mapeamento de <see cref="OperationLog"/> contra a tabela <c>operation_log</c> do modelo de dados v02
/// (secções 5.2, 5.3, 6.15 e 7.1), verificado com o fornecedor Npgsql e sem abrir ligação, e com as configurações do SQLite, onde o
/// <c>CHECK</c> e a identidade <c>GENERATED ALWAYS</c>, específicos do PostgreSQL, não são declarados.
/// </summary>
public sealed class OperationLogConfigurationTests
{
    private static readonly StoreObjectIdentifier Table = StoreObjectIdentifier.Table("operation_log");

    /// <summary>
    /// A tabela <c>operation_log</c> tem exatamente as cinco colunas do modelo de dados, sem o tipo da entidade (DM-19).
    /// </summary>
    [Fact]
    public void MapsExactlyTheColumnsOfTheDataModel()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("operation_log", entity.GetTableName());
        Assert.Equal(
            ["entity_id", "id", "occurred_at", "operation", "user_id"],
            entity.GetProperties().Select(property => property.GetColumnName(Table)!).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Tipos das colunas do dicionário de dados (secção 6.15), com o tipo enumerado <c>operation_type</c>.
    /// </summary>
    [Fact]
    public void MapsColumnTypes()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("bigint", ColumnType(entity, nameof(OperationLog.Id)));
        Assert.Equal("uuid", ColumnType(entity, nameof(OperationLog.UserId)));
        Assert.Equal("operation_type", ColumnType(entity, nameof(OperationLog.Operation)));
        Assert.Equal("uuid", ColumnType(entity, nameof(OperationLog.EntityId)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(OperationLog.OccurredAt)));
    }

    /// <summary>
    /// Só o autor admite <c>NULL</c>.
    /// </summary>
    [Fact]
    public void OnlyTheAuthorIsNullable()
    {
        Assert.Equal(
            [nameof(OperationLog.UserId)],
            GetEntity().GetProperties().Where(property => property.IsNullable).Select(property => property.Name).ToArray());
    }

    /// <summary>
    /// O identificador é <c>bigint</c> com identidade <c>GENERATED ALWAYS</c>, gerado ao inserir.
    /// </summary>
    [Fact]
    public void IdIsABigintGeneratedAlwaysIdentity()
    {
        IProperty id = GetEntity().FindProperty(nameof(OperationLog.Id))!;

        Assert.Equal(typeof(long), id.ClrType);
        Assert.Equal(ValueGenerated.OnAdd, id.ValueGenerated);
        Assert.Equal(NpgsqlValueGenerationStrategy.IdentityAlwaysColumn, id.GetValueGenerationStrategy());
        Assert.Equal("pk_operation_log", GetEntity().FindPrimaryKey()!.GetName());
        Assert.Equal("id", Assert.Single(GetEntity().FindPrimaryKey()!.Properties).GetColumnName());
    }

    /// <summary>
    /// Predefinido <c>now()</c> na data da operação; o identificador não tem predefinido, porque a identidade o gera.
    /// </summary>
    [Fact]
    public void MapsDefaults()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("now()", entity.FindProperty(nameof(OperationLog.OccurredAt))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(OperationLog.OccurredAt))!.ValueGenerated);
        Assert.Null(entity.FindProperty(nameof(OperationLog.Id))!.GetDefaultValueSql());
        Assert.Null(entity.FindProperty(nameof(OperationLog.Operation))!.GetDefaultValueSql());
    }

    /// <summary>
    /// A única chave estrangeira é a do autor para <c>app_user</c>, opcional e sem eliminação em cascata. A entidade
    /// registada não tem chave estrangeira, porque o seu tipo varia com a operação (DM-19).
    /// </summary>
    [Fact]
    public void MapsTheAuthorForeignKey()
    {
        IForeignKey key = Assert.Single(GetEntity().GetForeignKeys());

        Assert.Equal(typeof(AppUser), key.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(OperationLog.UserId), Assert.Single(key.Properties).Name);
        Assert.Equal("fk_operation_log_app_user_user_id", key.GetConstraintName());
        Assert.False(key.IsRequired);
        Assert.False(key.IsUnique);
        Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior);
    }

    /// <summary>
    /// Os índices são exatamente os dois da secção 5.3, sem filtro: por entidade e por operação.
    /// </summary>
    [Fact]
    public void MapsExactlyTheIndexesOfTheDataModel()
    {
        Dictionary<string, IIndex> indexes = GetEntity().GetIndexes().ToDictionary(index => index.GetDatabaseName()!);

        Assert.Equal(["ix_operation_log_entity", "ix_operation_log_occurred"], indexes.Keys.Order(StringComparer.Ordinal).ToArray());

        Assert.Equal(["entity_id", "occurred_at"], Columns(indexes["ix_operation_log_entity"]));
        Assert.Equal(["operation", "occurred_at"], Columns(indexes["ix_operation_log_occurred"]));
        Assert.All(indexes.Values, index =>
        {
            Assert.False(index.IsUnique);
            Assert.Null(index.GetFilter());
        });
    }

    /// <summary>
    /// O <c>CHECK</c> do autor: só falta quando a operação é <c>JOB_CLOSED</c> (secção 7.1).
    /// </summary>
    [Fact]
    public void DeclaresTheAuthorCheckConstraint()
    {
        ICheckConstraint check = Assert.Single(GetEntity().GetCheckConstraints());

        Assert.Equal("ck_operation_log_author", check.Name);
        Assert.Equal("user_id IS NOT NULL OR operation = 'JOB_CLOSED'", check.Sql);
    }

    /// <summary>
    /// A entidade não tem filtro global nem navegações.
    /// </summary>
    [Fact]
    public void HasNoGlobalQueryFilterAndNoNavigations()
    {
        IEntityType entity = GetEntity();

        Assert.Empty(entity.GetDeclaredQueryFilters());
        Assert.Empty(entity.GetNavigations());
    }

    /// <summary>
    /// Com o SQLite, a tabela e as colunas são as mesmas, mas o <c>CHECK</c> e a identidade <c>GENERATED ALWAYS</c>, que
    /// são do PostgreSQL, não são declarados: o identificador é uma chave gerada ao inserir.
    /// </summary>
    [Fact]
    public void WithoutNpgsqlTheCheckConstraintAndTheAlwaysIdentityAreNotDeclared()
    {
        IEntityType entity = ModelWithoutPostgresFeatures.GetEntity<OperationLog>();
        IProperty id = entity.FindProperty(nameof(OperationLog.Id))!;

        Assert.Equal("operation_log", entity.GetTableName());
        Assert.Empty(entity.GetCheckConstraints());
        Assert.Equal(ValueGenerated.OnAdd, id.ValueGenerated);
        Assert.NotEqual(NpgsqlValueGenerationStrategy.IdentityAlwaysColumn, id.GetValueGenerationStrategy());
        Assert.Equal(2, entity.GetIndexes().Count());
        Assert.Single(entity.GetForeignKeys());
    }

    private static string[] Columns(IIndex index)
    {
        return [.. index.Properties.Select(property => property.GetColumnName(Table)!)];
    }

    private static string? ColumnType(IEntityType entity, string property)
    {
        return entity.FindProperty(property)!.GetColumnType();
    }

    private static IEntityType GetEntity()
    {
        var builder = new DbContextOptionsBuilder<KatchDbContext>();
        builder.UseKatchPostgres("Host=localhost;Database=katch");

        using var context = new KatchDbContext(builder.Options);
        return context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(OperationLog))!;
    }
}
