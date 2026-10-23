// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Infrastructure.Persistence.Migrations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Katch.Backend.UnitTests.Infrastructure.Persistence;

/// <summary>
/// Testes das operações da migration <see cref="AddAdmin"/>, sem base de dados: criação da tabela <c>admin</c> e da
/// conta de Administrador dos dados iniciais, e a sua remoção ao reverter.
/// </summary>
public sealed class AddAdminMigrationTests
{
    private const string Password = "Katch2026admin";

    /// <summary>
    /// O <c>Up</c> cria a tabela <c>admin</c>, com a chave primária partilhada com <c>app_user</c>, e depois a conta
    /// de Administrador, com o resumo da palavra-passe e nunca a palavra-passe.
    /// </summary>
    [Fact]
    public void UpCreatesTheTableAndThenTheAdminAccount()
    {
        var migration = new AddAdmin(() => Password);

        Assert.Equal(2, migration.UpOperations.Count);

        CreateTableOperation table = Assert.IsType<CreateTableOperation>(migration.UpOperations[0]);
        Assert.Equal("admin", table.Name);
        Assert.Equal("id", Assert.Single(table.Columns).Name);
        Assert.Equal("pk_admin", table.PrimaryKey!.Name);
        AddForeignKeyOperation foreignKey = Assert.Single(table.ForeignKeys);
        Assert.Equal("fk_admin_app_user_id", foreignKey.Name);
        Assert.Equal("app_user", foreignKey.PrincipalTable);

        SqlOperation seed = Assert.IsType<SqlOperation>(migration.UpOperations[1]);
        Assert.Contains("INSERT INTO admin (id)", seed.Sql, StringComparison.Ordinal);
        Assert.Contains($"'{AdminAccountSeed.Email}'", seed.Sql, StringComparison.Ordinal);
        Assert.Contains("'ADMIN', 'ACTIVE'", seed.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, seed.Sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// O resumo gravado pelo <c>Up</c> é verificado com a palavra-passe da variável de ambiente.
    /// </summary>
    [Fact]
    public void SeededHashMatchesThePassword()
    {
        var migration = new AddAdmin(() => Password);

        string sql = Assert.IsType<SqlOperation>(migration.UpOperations[1]).Sql;
        string hash = sql.Split('\'')[3];

        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<AppUser>().VerifyHashedPassword(null!, hash, Password));
    }

    /// <summary>
    /// Sem palavra-passe, o <c>Up</c> falha em vez de criar uma conta de Administrador sem palavra-passe.
    /// </summary>
    [Fact]
    public void UpWithoutPasswordFails()
    {
        var migration = new AddAdmin(() => AdminAccountSeed.ReadPassword(_ => null));

        Assert.Throws<InvalidOperationException>(() => migration.UpOperations);
    }

    /// <summary>
    /// O <c>Down</c> apaga a tabela <c>admin</c> e depois as contas de Administrador, sem precisar da palavra-passe.
    /// </summary>
    [Fact]
    public void DownDropsTheTableAndThenTheAdminAccounts()
    {
        var migration = new AddAdmin(() => throw new InvalidOperationException("Não deve ser lida."));

        Assert.Equal(2, migration.DownOperations.Count);
        Assert.Equal("admin", Assert.IsType<DropTableOperation>(migration.DownOperations[0]).Name);
        Assert.Equal(AdminAccountSeed.DeleteSql, Assert.IsType<SqlOperation>(migration.DownOperations[1]).Sql);
    }
}
