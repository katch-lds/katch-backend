// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Katch.Backend.UnitTests.Repositories.Configuration;

/// <summary>
/// Testes do mapeamento de <see cref="Message"/> contra a tabela <c>message</c> do modelo de dados v02 (secções 5.2,
/// 5.3, 6.14 e 7.1), verificado com o fornecedor Npgsql e sem abrir ligação, e com as configurações do SQLite, onde os <c>CHECK</c> e o
/// filtro do índice, específicos do PostgreSQL, não são declarados.
/// </summary>
public sealed class MessageConfigurationTests
{
    private static readonly StoreObjectIdentifier Table = StoreObjectIdentifier.Table("message");

    /// <summary>
    /// A tabela <c>message</c> tem exatamente as seis colunas do modelo de dados.
    /// </summary>
    [Fact]
    public void MapsExactlyTheColumnsOfTheDataModel()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("message", entity.GetTableName());
        Assert.Equal(
            ["content", "id", "match_id", "read_at", "sender_id", "sent_at"],
            entity.GetProperties().Select(property => property.GetColumnName(Table)!).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Tipos das colunas do dicionário de dados (secção 6.14), com o texto em <c>varchar(1000)</c>.
    /// </summary>
    [Fact]
    public void MapsColumnTypesAndLength()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("uuid", ColumnType(entity, nameof(Message.Id)));
        Assert.Equal("uuid", ColumnType(entity, nameof(Message.MatchId)));
        Assert.Equal("uuid", ColumnType(entity, nameof(Message.SenderId)));
        Assert.Equal("character varying(1000)", ColumnType(entity, nameof(Message.Content)));
        Assert.Equal(1000, entity.FindProperty(nameof(Message.Content))!.GetMaxLength());
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Message.SentAt)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Message.ReadAt)));
    }

    /// <summary>
    /// Só a data de leitura admite <c>NULL</c>.
    /// </summary>
    [Fact]
    public void OnlyTheReadDateIsNullable()
    {
        Assert.Equal(
            [nameof(Message.ReadAt)],
            GetEntity().GetProperties().Where(property => property.IsNullable).Select(property => property.Name).ToArray());
    }

    /// <summary>
    /// Predefinidos: <c>gen_random_uuid()</c> no identificador e <c>now()</c> na data de envio; a data de leitura e o
    /// texto não têm predefinido.
    /// </summary>
    [Fact]
    public void MapsDefaults()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("gen_random_uuid()", entity.FindProperty(nameof(Message.Id))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Message.Id))!.ValueGenerated);
        Assert.Equal("now()", entity.FindProperty(nameof(Message.SentAt))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Message.SentAt))!.ValueGenerated);
        Assert.Null(entity.FindProperty(nameof(Message.ReadAt))!.GetDefaultValueSql());
        Assert.Null(entity.FindProperty(nameof(Message.Content))!.GetDefaultValueSql());
    }

    /// <summary>
    /// As duas chaves estrangeiras, ambas obrigatórias e sem eliminação em cascata: o match e o remetente.
    /// </summary>
    [Fact]
    public void MapsForeignKeys()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("id", Assert.Single(entity.FindPrimaryKey()!.Properties).GetColumnName());
        Assert.Equal("pk_message", entity.FindPrimaryKey()!.GetName());
        Assert.Equal(2, entity.GetForeignKeys().Count());

        AssertForeignKey(entity, typeof(Match), nameof(Message.MatchId), "fk_message_match_match_id");
        AssertForeignKey(entity, typeof(AppUser), nameof(Message.SenderId), "fk_message_app_user_sender_id");
    }

    /// <summary>
    /// Os índices são exatamente os dois da secção 5.3: o histórico da conversa e as mensagens por ler, este com o
    /// filtro <c>read_at IS NULL</c>.
    /// </summary>
    [Fact]
    public void MapsExactlyTheIndexesOfTheDataModel()
    {
        Dictionary<string, IIndex> indexes = GetEntity().GetIndexes().ToDictionary(index => index.GetDatabaseName()!);

        Assert.Equal(["ix_message_match_sent", "ix_message_unread"], indexes.Keys.Order(StringComparer.Ordinal).ToArray());

        Assert.Equal(["match_id", "sent_at"], Columns(indexes["ix_message_match_sent"]));
        Assert.Null(indexes["ix_message_match_sent"].GetFilter());
        Assert.False(indexes["ix_message_match_sent"].IsUnique);

        Assert.Equal(["match_id"], Columns(indexes["ix_message_unread"]));
        Assert.Equal("read_at IS NULL", indexes["ix_message_unread"].GetFilter());
        Assert.False(indexes["ix_message_unread"].IsUnique);
    }

    /// <summary>
    /// Os dois <c>CHECK</c> da tabela: texto sem ser só espaços (<c>ck_message_content</c>, nome acrescentado pelo
    /// mapeamento) e leitura nunca anterior ao envio (<c>ck_message_read</c>).
    /// </summary>
    [Fact]
    public void DeclaresTheMessageCheckConstraints()
    {
        Dictionary<string, string> checks = GetEntity().GetCheckConstraints()
            .ToDictionary(check => check.Name!, check => check.Sql);

        Assert.Equal(["ck_message_content", "ck_message_read"], checks.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("btrim(content) <> ''", checks["ck_message_content"]);
        Assert.Equal("read_at IS NULL OR read_at >= sent_at", checks["ck_message_read"]);
    }

    /// <summary>
    /// A entidade não tem filtro global nem navegações: as mensagens não desaparecem das consultas.
    /// </summary>
    [Fact]
    public void HasNoGlobalQueryFilterAndNoNavigations()
    {
        IEntityType entity = GetEntity();

        Assert.Empty(entity.GetDeclaredQueryFilters());
        Assert.Empty(entity.GetNavigations());
    }

    /// <summary>
    /// Com o SQLite, a tabela e as colunas são as mesmas, mas os <c>CHECK</c> e o filtro do índice, que são SQL do
    /// PostgreSQL, não são declarados.
    /// </summary>
    [Fact]
    public void WithoutNpgsqlTheCheckConstraintsAndTheIndexFilterAreNotDeclared()
    {
        IEntityType entity = ModelWithoutPostgresFeatures.GetEntity<Message>();

        Assert.Equal("message", entity.GetTableName());
        Assert.Empty(entity.GetCheckConstraints());
        Assert.Null(entity.GetIndexes().Single(index => index.GetDatabaseName() == "ix_message_unread").GetFilter());
        Assert.Equal(
            ["ix_message_match_sent", "ix_message_unread"],
            entity.GetIndexes().Select(index => index.GetDatabaseName()!).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(2, entity.GetForeignKeys().Count());
    }

    private static void AssertForeignKey(IEntityType entity, Type principal, string property, string constraintName)
    {
        IForeignKey key = Assert.Single(entity.GetForeignKeys(), candidate => candidate.PrincipalEntityType.ClrType == principal);

        Assert.Equal(property, Assert.Single(key.Properties).Name);
        Assert.Equal(constraintName, key.GetConstraintName());
        Assert.True(key.IsRequired);
        Assert.False(key.IsUnique);
        Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior);
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
        return context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Message))!;
    }
}
