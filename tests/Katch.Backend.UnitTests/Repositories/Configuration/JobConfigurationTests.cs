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
/// Testes do mapeamento de <see cref="Job"/> contra a tabela <c>job</c> do modelo de dados v02 (secções 5.1, 5.2, 5.3,
/// 6.11 e 7.1), verificado com o fornecedor Npgsql e sem abrir ligação. O filtro das vagas eliminadas é verificado nos
/// testes de integração.
/// </summary>
public sealed class JobConfigurationTests
{
    private static readonly StoreObjectIdentifier Table = StoreObjectIdentifier.Table("job");

    /// <summary>
    /// A tabela <c>job</c> tem exatamente as 17 colunas do modelo de dados, sem <c>closed_at</c> nem
    /// <c>close_reason</c> (DM-23) e sem datas de criação nem de alteração (DM-22).
    /// </summary>
    [Fact]
    public void MapsExactlyTheColumnsOfTheDataModel()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("job", entity.GetTableName());
        Assert.Equal(
            [
                "company_id", "contract_type", "deleted_at", "description", "expires_at", "first_published_at", "id",
                "is_urgent", "location_id", "max_salary", "min_salary", "photo_paths", "published_at", "status",
                "suspended_at", "title", "work_mode",
            ],
            entity.GetProperties().Select(property => property.GetColumnName(Table)!).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Tipos das colunas do dicionário de dados (secção 6.11).
    /// </summary>
    [Fact]
    public void MapsColumnTypes()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("uuid", ColumnType(entity, nameof(Job.Id)));
        Assert.Equal("uuid", ColumnType(entity, nameof(Job.CompanyId)));
        Assert.Equal("character varying(150)", ColumnType(entity, nameof(Job.Title)));
        Assert.Equal("text", ColumnType(entity, nameof(Job.Description)));
        Assert.Equal("numeric(10,2)", ColumnType(entity, nameof(Job.MinSalary)));
        Assert.Equal("numeric(10,2)", ColumnType(entity, nameof(Job.MaxSalary)));
        Assert.Equal("uuid", ColumnType(entity, nameof(Job.LocationId)));
        Assert.Equal("contract_type", ColumnType(entity, nameof(Job.ContractType)));
        Assert.Equal("work_mode", ColumnType(entity, nameof(Job.WorkMode)));
        Assert.Equal("boolean", ColumnType(entity, nameof(Job.IsUrgent)));
        Assert.Equal("character varying(500)[]", ColumnType(entity, nameof(Job.PhotoPaths)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Job.ExpiresAt)));
        Assert.Equal("job_status", ColumnType(entity, nameof(Job.Status)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Job.FirstPublishedAt)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Job.PublishedAt)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Job.SuspendedAt)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Job.DeletedAt)));
    }

    /// <summary>
    /// Só a data-limite e as datas de publicação, suspensão e eliminação admitem <c>NULL</c>.
    /// </summary>
    [Fact]
    public void OnlyTheOptionalColumnsAreNullable()
    {
        Assert.Equal(
            [
                nameof(Job.DeletedAt), nameof(Job.ExpiresAt), nameof(Job.FirstPublishedAt), nameof(Job.PublishedAt),
                nameof(Job.SuspendedAt),
            ],
            GetEntity().GetProperties().Where(property => property.IsNullable).Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Predefinidos: <c>gen_random_uuid()</c>, <c>is_urgent</c> falso, lista de fotografias vazia e estado
    /// <c>UNPUBLISHED</c>.
    /// </summary>
    [Fact]
    public void MapsDefaults()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("gen_random_uuid()", entity.FindProperty(nameof(Job.Id))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Job.Id))!.ValueGenerated);
        Assert.False(Assert.IsType<bool>(entity.FindProperty(nameof(Job.IsUrgent))!.GetDefaultValue()));
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Job.IsUrgent))!.ValueGenerated);
        Assert.Equal("'{}'", entity.FindProperty(nameof(Job.PhotoPaths))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Job.PhotoPaths))!.ValueGenerated);
        Assert.Equal(JobStatus.Unpublished, entity.FindProperty(nameof(Job.Status))!.GetDefaultValue());
    }

    /// <summary>
    /// <c>company_id</c> referencia <c>company</c> e <c>location_id</c> referencia <c>location</c>, sem eliminação em
    /// cascata.
    /// </summary>
    [Fact]
    public void MapsForeignKeys()
    {
        IEntityType entity = GetEntity();

        Assert.Equal(2, entity.GetForeignKeys().Count());

        IForeignKey company = Assert.Single(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Company));
        Assert.Equal(nameof(Job.CompanyId), Assert.Single(company.Properties).Name);
        Assert.Equal("fk_job_company_company_id", company.GetConstraintName());
        Assert.False(company.IsUnique);
        Assert.True(company.IsRequired);
        Assert.Equal(DeleteBehavior.NoAction, company.DeleteBehavior);

        IForeignKey location = Assert.Single(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Location));
        Assert.Equal(nameof(Job.LocationId), Assert.Single(location.Properties).Name);
        Assert.Equal("fk_job_location_location_id", location.GetConstraintName());
        Assert.True(location.IsRequired);
        Assert.Equal(DeleteBehavior.NoAction, location.DeleteBehavior);
    }

    /// <summary>
    /// Os índices são exatamente os da secção 5.3, com os filtros das vagas não eliminadas e das vagas publicadas.
    /// </summary>
    [Fact]
    public void MapsExactlyTheIndexesOfTheDataModel()
    {
        Dictionary<string, IIndex> indexes = GetEntity().GetIndexes().ToDictionary(index => index.GetDatabaseName()!);

        Assert.Equal(["ix_job_company_status", "ix_job_expiry", "ix_job_location"], indexes.Keys.Order(StringComparer.Ordinal).ToArray());

        Assert.Equal(["company_id", "status"], indexes["ix_job_company_status"].Properties.Select(property => property.GetColumnName(Table)!).ToArray());
        Assert.Equal("deleted_at IS NULL", indexes["ix_job_company_status"].GetFilter());

        Assert.Equal("location_id", Assert.Single(indexes["ix_job_location"].Properties).GetColumnName(Table));
        Assert.Equal("deleted_at IS NULL", indexes["ix_job_location"].GetFilter());

        Assert.Equal("expires_at", Assert.Single(indexes["ix_job_expiry"].Properties).GetColumnName(Table));
        Assert.Equal("status = 'PUBLISHED'", indexes["ix_job_expiry"].GetFilter());

        Assert.All(indexes.Values, index => Assert.False(index.IsUnique));
    }

    /// <summary>
    /// Os <c>CHECK</c> da tabela <c>job</c> (secções 5.2 e 7.1): textos não vazios, intervalo salarial positivo e
    /// coerente, até 5 fotografias e primeira publicação preenchida nas vagas publicadas ou suspensas.
    /// </summary>
    [Fact]
    public void DeclaresTheJobCheckConstraints()
    {
        Dictionary<string, string> checks = GetEntity().GetCheckConstraints()
            .ToDictionary(check => check.Name!, check => check.Sql);

        Assert.Equal(6, checks.Count);
        Assert.Equal("btrim(title) <> ''", checks["ck_job_title"]);
        Assert.Equal("btrim(description) <> ''", checks["ck_job_description"]);
        Assert.Equal("min_salary > 0", checks["ck_job_min_salary"]);
        Assert.Equal("max_salary >= min_salary", checks["ck_job_salary"]);
        Assert.Equal("cardinality(photo_paths) <= 5", checks["ck_job_photo_paths"]);
        Assert.Equal(
            "status NOT IN ('PUBLISHED', 'SUSPENDED') OR first_published_at IS NOT NULL",
            checks["ck_job_published"]);
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
        return context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Job))!;
    }
}
