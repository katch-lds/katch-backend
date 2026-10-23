// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Katch.Backend.UnitTests.Repositories.Configuration;

/// <summary>
/// Testes do mapeamento de <see cref="Admin"/> contra a tabela <c>admin</c> do modelo de dados v02 (secções 5.2 e 6.2),
/// verificado com o fornecedor Npgsql e sem abrir ligação.
/// </summary>
public sealed class AdminConfigurationTests
{
    /// <summary>
    /// A tabela <c>admin</c> só tem a coluna <c>id</c>, que é a chave primária e não é gerada pela base de dados.
    /// </summary>
    [Fact]
    public void MapsTableWithSharedPrimaryKey()
    {
        IEntityType entity = GetAdminEntity();
        var table = StoreObjectIdentifier.Table("admin");

        Assert.Equal("admin", entity.GetTableName());
        Assert.Equal("id", Assert.Single(entity.GetProperties()).GetColumnName(table));

        IProperty id = Assert.Single(entity.FindPrimaryKey()!.Properties);
        Assert.Equal("id", id.GetColumnName(table));
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        Assert.Equal("pk_admin", entity.FindPrimaryKey()!.GetName());
    }

    /// <summary>
    /// A chave primária é também chave estrangeira para <c>app_user</c>, numa relação 1 : 0..1 sem eliminação em cascata.
    /// </summary>
    [Fact]
    public void KeyIsForeignKeyToAppUserWithoutCascade()
    {
        IEntityType entity = GetAdminEntity();

        IForeignKey foreignKey = Assert.Single(entity.GetForeignKeys());

        Assert.Equal(typeof(AppUser), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal("id", Assert.Single(foreignKey.Properties).GetColumnName());
        Assert.Equal("fk_admin_app_user_id", foreignKey.GetConstraintName());
        Assert.True(foreignKey.IsUnique);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior);
    }

    private static IEntityType GetAdminEntity()
    {
        var builder = new DbContextOptionsBuilder<KatchDbContext>();
        builder.UseKatchPostgres("Host=localhost;Database=katch");

        using var context = new KatchDbContext(builder.Options);
        IModel model = context.GetService<IDesignTimeModel>().Model;
        return model.FindEntityType(typeof(Admin))!;
    }
}
