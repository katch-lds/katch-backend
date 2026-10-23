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
/// Testes do mapeamento de <see cref="Company"/> contra a tabela <c>company</c> do modelo de dados v02 (secções 5.2,
/// 5.3, 6.10 e 7.1). O modelo é verificado com o fornecedor Npgsql, sem abrir ligação, porque o SQLite dos testes de
/// integração não aplica os <c>CHECK</c>, os tipos enumerados nem os arrays do PostgreSQL.
/// </summary>
public sealed class CompanyConfigurationTests
{
    private static readonly StoreObjectIdentifier Table = StoreObjectIdentifier.Table("company");

    /// <summary>
    /// A tabela <c>company</c> tem exatamente as 17 colunas do modelo de dados, sem datas de criação nem de alteração
    /// (DM-22) e sem <c>status_changed_*</c> (DM-18).
    /// </summary>
    [Fact]
    public void MapsExactlyTheColumnsOfTheDataModel()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("company", entity.GetTableName());
        Assert.Equal(
            [
                "address", "company_name", "contact_email", "contact_phone", "description", "id", "industry", "location_id",
                "logo_path", "photo_paths", "recruiter_id", "responsible_name", "status", "status_reason", "submitted_at",
                "tax_id", "website",
            ],
            entity.GetProperties().Select(property => property.GetColumnName(Table)!).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Tipos das colunas: <c>varchar</c> com os comprimentos do dicionário de dados, tipos enumerados, array de caminhos
    /// e <c>timestamptz</c>.
    /// </summary>
    [Fact]
    public void MapsColumnTypes()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("uuid", ColumnType(entity, nameof(Company.Id)));
        Assert.Equal("uuid", ColumnType(entity, nameof(Company.RecruiterId)));
        Assert.Equal("character varying(200)", ColumnType(entity, nameof(Company.CompanyName)));
        Assert.Equal("character varying(9)", ColumnType(entity, nameof(Company.TaxId)));
        Assert.Equal("industry", ColumnType(entity, nameof(Company.Industry)));
        Assert.Equal("character varying(255)", ColumnType(entity, nameof(Company.Address)));
        Assert.Equal("uuid", ColumnType(entity, nameof(Company.LocationId)));
        Assert.Equal("character varying(255)", ColumnType(entity, nameof(Company.ContactEmail)));
        Assert.Equal("character varying(9)", ColumnType(entity, nameof(Company.ContactPhone)));
        Assert.Equal("character varying(150)", ColumnType(entity, nameof(Company.ResponsibleName)));
        Assert.Equal("character varying(500)", ColumnType(entity, nameof(Company.Website)));
        Assert.Equal("character varying(1000)", ColumnType(entity, nameof(Company.Description)));
        Assert.Equal("character varying(500)", ColumnType(entity, nameof(Company.LogoPath)));
        Assert.Equal("character varying(500)[]", ColumnType(entity, nameof(Company.PhotoPaths)));
        Assert.Equal("company_status", ColumnType(entity, nameof(Company.Status)));
        Assert.Equal("character varying(500)", ColumnType(entity, nameof(Company.StatusReason)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Company.SubmittedAt)));
    }

    /// <summary>
    /// Só o sítio na Internet, a descrição, o logótipo e o motivo admitem <c>NULL</c>.
    /// </summary>
    [Fact]
    public void OnlyTheOptionalColumnsAreNullable()
    {
        Assert.Equal(
            [nameof(Company.Description), nameof(Company.LogoPath), nameof(Company.StatusReason), nameof(Company.Website)],
            GetEntity().GetProperties().Where(property => property.IsNullable).Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Predefinidos: <c>gen_random_uuid()</c>, galeria vazia, estado <c>PENDING</c> e data de submissão <c>now()</c>.
    /// </summary>
    [Fact]
    public void MapsDefaults()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("gen_random_uuid()", entity.FindProperty(nameof(Company.Id))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Company.Id))!.ValueGenerated);
        Assert.Equal("'{}'", entity.FindProperty(nameof(Company.PhotoPaths))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Company.PhotoPaths))!.ValueGenerated);
        Assert.Equal(CompanyStatus.Pending, entity.FindProperty(nameof(Company.Status))!.GetDefaultValue());
        Assert.Equal("now()", entity.FindProperty(nameof(Company.SubmittedAt))!.GetDefaultValueSql());
    }

    /// <summary>
    /// <c>recruiter_id</c> referencia <c>recruiter</c> numa relação 1 : 0..1 e <c>location_id</c> referencia
    /// <c>location</c>; nenhuma das duas elimina em cascata.
    /// </summary>
    [Fact]
    public void MapsForeignKeys()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("id", Assert.Single(entity.FindPrimaryKey()!.Properties).GetColumnName());
        Assert.Equal(2, entity.GetForeignKeys().Count());

        IForeignKey recruiter = Assert.Single(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Recruiter));
        Assert.Equal(nameof(Company.RecruiterId), Assert.Single(recruiter.Properties).Name);
        Assert.True(recruiter.IsUnique);
        Assert.True(recruiter.IsRequired);
        Assert.Equal(DeleteBehavior.NoAction, recruiter.DeleteBehavior);

        IForeignKey location = Assert.Single(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Location));
        Assert.Equal(nameof(Company.LocationId), Assert.Single(location.Properties).Name);
        Assert.False(location.IsUnique);
        Assert.True(location.IsRequired);
        Assert.Equal(DeleteBehavior.NoAction, location.DeleteBehavior);
    }

    /// <summary>
    /// Os índices são exatamente os do modelo de dados: <c>uq_company_recruiter</c>, <c>uq_company_tax_id</c> e
    /// <c>ix_company_status</c>, sem índice próprio para <c>location_id</c>.
    /// </summary>
    [Fact]
    public void MapsExactlyTheIndexesOfTheDataModel()
    {
        Dictionary<string, IIndex> indexes = GetEntity().GetIndexes().ToDictionary(index => index.GetDatabaseName()!);

        Assert.Equal(["ix_company_status", "uq_company_recruiter", "uq_company_tax_id"], indexes.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("recruiter_id", Assert.Single(indexes["uq_company_recruiter"].Properties).GetColumnName(Table));
        Assert.True(indexes["uq_company_recruiter"].IsUnique);
        Assert.Equal("tax_id", Assert.Single(indexes["uq_company_tax_id"].Properties).GetColumnName(Table));
        Assert.True(indexes["uq_company_tax_id"].IsUnique);
        Assert.Equal("status", Assert.Single(indexes["ix_company_status"].Properties).GetColumnName(Table));
        Assert.False(indexes["ix_company_status"].IsUnique);
    }

    /// <summary>
    /// Os <c>CHECK</c> da tabela <c>company</c>, incluindo o motivo só no estado recusada e com 10 a 500 caracteres.
    /// </summary>
    [Fact]
    public void DeclaresTheCompanyCheckConstraints()
    {
        Dictionary<string, string> checks = GetEntity().GetCheckConstraints()
            .ToDictionary(check => check.Name!, check => check.Sql);

        Assert.Equal(10, checks.Count);
        Assert.Equal("btrim(company_name) <> ''", checks["ck_company_company_name"]);
        Assert.Equal("tax_id ~ '^[0-9]{9}$'", checks["ck_company_tax_id"]);
        Assert.Equal("btrim(address) <> ''", checks["ck_company_address"]);
        Assert.Equal("contact_email ~ '^[^@[:space:]]+@[^@[:space:]]+$'", checks["ck_company_contact_email"]);
        Assert.Equal("contact_phone ~ '^[0-9]{9}$'", checks["ck_company_contact_phone"]);
        Assert.Equal("btrim(responsible_name) <> ''", checks["ck_company_responsible_name"]);
        Assert.Equal("website ~* '^https?://'", checks["ck_company_website"]);
        Assert.Equal("cardinality(photo_paths) <= 6", checks["ck_company_photo_paths"]);
        Assert.Equal("(status = 'REJECTED') = (status_reason IS NOT NULL)", checks["ck_company_reason"]);
        Assert.Equal(
            "status_reason IS NULL OR char_length(btrim(status_reason)) BETWEEN 10 AND 500",
            checks["ck_company_reason_length"]);
    }

    /// <summary>
    /// Sem o índice automático das chaves estrangeiras, as tabelas anteriores mantêm os índices do modelo de dados.
    /// </summary>
    [Fact]
    public void ForeignKeysDoNotCreateIndexesOutsideTheDataModel()
    {
        IModel model = GetModel();

        Assert.Equal(
            ["ix_candidate_location"],
            model.FindEntityType(typeof(Candidate))!.GetIndexes().Select(index => index.GetDatabaseName()!).ToArray());
        Assert.Equal(
            ["uq_candidate_link_position", "uq_candidate_link_url"],
            model.FindEntityType(typeof(CandidateLink))!.GetIndexes().Select(index => index.GetDatabaseName()!).Order(StringComparer.Ordinal).ToArray());
        Assert.Empty(model.FindEntityType(typeof(Recruiter))!.GetIndexes());
    }

    private static string? ColumnType(IEntityType entity, string property)
    {
        return entity.FindProperty(property)!.GetColumnType();
    }

    private static IEntityType GetEntity()
    {
        return GetModel().FindEntityType(typeof(Company))!;
    }

    private static IModel GetModel()
    {
        var builder = new DbContextOptionsBuilder<KatchDbContext>();
        builder.UseKatchPostgres("Host=localhost;Database=katch");

        using var context = new KatchDbContext(builder.Options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
