// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Katch.Backend.UnitTests.Infrastructure.Persistence;

/// <summary>
/// Testes das operações da migration <see cref="AddAppUser"/>. O índice único sobre <c>lower(email)</c> é criado por SQL
/// e não consta do snapshot, por isso o EF Core não o repõe se for apagado da migration; estes testes fixam o <c>Up</c>
/// e o <c>Down</c> sem precisar de uma base de dados.
/// </summary>
public sealed class AddAppUserMigrationTests
{
    /// <summary>
    /// O <c>Up</c> cria a tabela <c>app_user</c> e depois o índice único sobre <c>lower(email)</c>.
    /// </summary>
    [Fact]
    public void UpCreatesTheTableAndThenTheCaseInsensitiveUniqueIndex()
    {
        var migration = new AddAppUser();

        CreateTableOperation table = Assert.IsType<CreateTableOperation>(migration.UpOperations[0]);
        SqlOperation index = Assert.IsType<SqlOperation>(migration.UpOperations[1]);

        Assert.Equal(2, migration.UpOperations.Count);
        Assert.Equal("app_user", table.Name);
        Assert.Equal("CREATE UNIQUE INDEX uq_app_user_email ON app_user (lower(email));", index.Sql);
    }

    /// <summary>
    /// O <c>Down</c> apaga primeiro o índice e depois a tabela.
    /// </summary>
    [Fact]
    public void DownDropsTheIndexAndThenTheTable()
    {
        var migration = new AddAppUser();

        SqlOperation index = Assert.IsType<SqlOperation>(migration.DownOperations[0]);
        DropTableOperation table = Assert.IsType<DropTableOperation>(migration.DownOperations[1]);

        Assert.Equal(2, migration.DownOperations.Count);
        Assert.Equal("DROP INDEX uq_app_user_email;", index.Sql);
        Assert.Equal("app_user", table.Name);
    }
}
