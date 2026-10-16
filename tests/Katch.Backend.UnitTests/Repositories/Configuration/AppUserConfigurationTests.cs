// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Katch.Backend.UnitTests.Repositories.Configuration;

/// <summary>
/// Testes do mapeamento de <see cref="AppUser"/> contra a tabela <c>app_user</c> do modelo de dados v02
/// (secções 5.2 e 6.1). Os testes de integração correm sobre SQLite, que não aplica os <c>CHECK</c> do PostgreSQL,
/// pelo que o modelo é verificado aqui com o fornecedor Npgsql, sem abrir ligação.
/// </summary>
public sealed class AppUserConfigurationTests
{
    /// <summary>
    /// A entidade é mapeada para a tabela <c>app_user</c> com as colunas, tipos e predefinidos do modelo de dados.
    /// </summary>
    [Fact]
    public void MapsTableColumnsAndDefaults()
    {
        IEntityType entity = GetAppUserEntity();
        var table = StoreObjectIdentifier.Table("app_user");

        Assert.Equal("app_user", entity.GetTableName());
        Assert.Equal(
            ["created_at", "email", "id", "password_hash", "status", "terms_accepted_at", "updated_at", "user_type"],
            entity.GetProperties().Select(property => property.GetColumnName(table)!).Order(StringComparer.Ordinal).ToArray());

        Assert.Equal("id", Assert.Single(entity.FindPrimaryKey()!.Properties).GetColumnName(table));
        Assert.Equal("gen_random_uuid()", entity.FindProperty(nameof(AppUser.Id))!.GetDefaultValueSql());
        Assert.Equal("now()", entity.FindProperty(nameof(AppUser.CreatedAt))!.GetDefaultValueSql());
        Assert.Equal("now()", entity.FindProperty(nameof(AppUser.UpdatedAt))!.GetDefaultValueSql());
        Assert.Equal(AccountStatus.Active, entity.FindProperty(nameof(AppUser.Status))!.GetDefaultValue());
        Assert.Equal("user_type", entity.FindProperty(nameof(AppUser.UserType))!.GetColumnType());
        Assert.Equal("account_status", entity.FindProperty(nameof(AppUser.Status))!.GetColumnType());
    }

    /// <summary>
    /// Obrigatoriedade e comprimentos das colunas: correio e resumo obrigatórios com 255 caracteres; a aceitação
    /// das condições é a única coluna que admite nulo.
    /// </summary>
    [Fact]
    public void MapsNullabilityAndLengths()
    {
        IEntityType entity = GetAppUserEntity();

        Assert.Equal(255, entity.FindProperty(nameof(AppUser.Email))!.GetMaxLength());
        Assert.Equal(255, entity.FindProperty(nameof(AppUser.PasswordHash))!.GetMaxLength());
        Assert.Equal(
            [nameof(AppUser.TermsAcceptedAt)],
            entity.GetProperties().Where(property => property.IsNullable).Select(property => property.Name).ToArray());
    }

    /// <summary>
    /// Os quatro <c>CHECK</c> do modelo de dados existem com a expressão da secção 5.2.
    /// </summary>
    [Fact]
    public void DeclaresTheCheckConstraintsOfTheDataModel()
    {
        IEntityType entity = GetAppUserEntity();

        Dictionary<string, string> checks = entity.GetCheckConstraints().ToDictionary(check => check.Name!, check => check.Sql);

        Assert.Equal(4, checks.Count);
        Assert.Equal("email ~ '^[^@[:space:]]+@[^@[:space:]]+$'", checks["ck_app_user_email"]);
        Assert.Equal("user_type = 'ADMIN' OR terms_accepted_at IS NOT NULL", checks["ck_app_user_terms"]);
        Assert.Equal("status <> 'SUSPENDED' OR user_type = 'CANDIDATE'", checks["ck_app_user_suspended"]);
        Assert.Equal("user_type <> 'ADMIN' OR status = 'ACTIVE'", checks["ck_app_user_admin_active"]);
    }

    private static IEntityType GetAppUserEntity()
    {
        var builder = new DbContextOptionsBuilder<KatchDbContext>();
        builder.UseKatchPostgres("Host=localhost;Database=katch");

        using var context = new KatchDbContext(builder.Options);
        IModel model = context.GetService<IDesignTimeModel>().Model;
        return model.FindEntityType(typeof(AppUser))!;
    }
}
