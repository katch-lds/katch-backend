// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Katch.Backend.UnitTests.Repositories.Configuration;

/// <summary>
/// Testes do mapeamento de <see cref="Candidate"/>, <see cref="Location"/> e <see cref="CandidateLink"/> contra as
/// tabelas <c>candidate</c>, <c>location</c> e <c>candidate_link</c> do modelo de dados v02 (secções 5.2, 5.3, 6.4 a 6.6
/// e 7.3). O modelo é verificado com o fornecedor Npgsql, sem abrir ligação, porque o SQLite dos testes de integração
/// não aplica os <c>CHECK</c> nem o <c>xmin</c> do PostgreSQL.
/// </summary>
public sealed class CandidateConfigurationTests
{
    /// <summary>
    /// A tabela <c>candidate</c> tem as 15 colunas do modelo de dados, mais o <c>xmin</c>, com os tipos e predefinidos certos.
    /// </summary>
    [Fact]
    public void MapsCandidateColumnsTypesAndDefaults()
    {
        IEntityType entity = GetEntity<Candidate>();
        var table = StoreObjectIdentifier.Table("candidate");

        Assert.Equal("candidate", entity.GetTableName());
        Assert.Equal(
            [
                "availability", "cv_path", "desired_role", "experience_summary", "full_name", "id", "location_id",
                "max_distance_km", "min_salary_expectation", "next_swipe_available_at", "phone_number",
                "preferred_contract_types", "preferred_work_modes", "profile_photo_path", "swipe_rights_remaining", "xmin",
            ],
            entity.GetProperties().Select(property => property.GetColumnName(table)!).Order(StringComparer.Ordinal).ToArray());

        Assert.Equal("work_mode[]", entity.FindProperty(nameof(Candidate.PreferredWorkModes))!.GetColumnType());
        Assert.Equal("contract_type[]", entity.FindProperty(nameof(Candidate.PreferredContractTypes))!.GetColumnType());
        Assert.Equal("availability", entity.FindProperty(nameof(Candidate.Availability))!.GetColumnType());
        Assert.Equal("'{}'", entity.FindProperty(nameof(Candidate.PreferredWorkModes))!.GetDefaultValueSql());
        Assert.Equal("'{}'", entity.FindProperty(nameof(Candidate.PreferredContractTypes))!.GetDefaultValueSql());
        Assert.Equal((short)10, entity.FindProperty(nameof(Candidate.SwipeRightsRemaining))!.GetDefaultValue());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Candidate.SwipeRightsRemaining))!.ValueGenerated);
        Assert.Equal("numeric(10,2)", entity.FindProperty(nameof(Candidate.MinSalaryExpectation))!.GetColumnType());
    }

    /// <summary>
    /// Comprimentos máximos e colunas obrigatórias do Candidato.
    /// </summary>
    [Fact]
    public void MapsCandidateLengthsAndNullability()
    {
        IEntityType entity = GetEntity<Candidate>();

        Assert.Equal(150, entity.FindProperty(nameof(Candidate.FullName))!.GetMaxLength());
        Assert.Equal(9, entity.FindProperty(nameof(Candidate.PhoneNumber))!.GetMaxLength());
        Assert.Equal(150, entity.FindProperty(nameof(Candidate.DesiredRole))!.GetMaxLength());
        Assert.Equal(1000, entity.FindProperty(nameof(Candidate.ExperienceSummary))!.GetMaxLength());
        Assert.Equal(500, entity.FindProperty(nameof(Candidate.ProfilePhotoPath))!.GetMaxLength());
        Assert.Equal(500, entity.FindProperty(nameof(Candidate.CvPath))!.GetMaxLength());
        Assert.Equal(
            [
                nameof(Candidate.Availability), nameof(Candidate.CvPath), nameof(Candidate.DesiredRole),
                nameof(Candidate.ExperienceSummary), nameof(Candidate.MaxDistanceKm), nameof(Candidate.MinSalaryExpectation),
                nameof(Candidate.NextSwipeAvailableAt), nameof(Candidate.ProfilePhotoPath),
            ],
            entity.GetProperties().Where(property => property.IsNullable).Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// A marca de concorrência é a coluna de sistema <c>xmin</c> (<see cref="Candidate.Version"/>), sem coluna própria.
    /// </summary>
    [Fact]
    public void VersionIsTheXminConcurrencyToken()
    {
        IProperty version = GetEntity<Candidate>().FindProperty(nameof(Candidate.Version))!;

        Assert.Equal("xmin", version.GetColumnName());
        Assert.Equal("xid", version.GetColumnType());
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
    }

    /// <summary>
    /// A chave primária é também chave estrangeira para <c>app_user</c>; <c>location_id</c> referencia <c>location</c>;
    /// nenhuma das duas elimina em cascata, e <c>ix_candidate_location</c> existe.
    /// </summary>
    [Fact]
    public void MapsCandidateKeysAndIndex()
    {
        IEntityType entity = GetEntity<Candidate>();

        Assert.Equal("id", Assert.Single(entity.FindPrimaryKey()!.Properties).GetColumnName());

        IForeignKey user = Assert.Single(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(AppUser));
        Assert.Equal(nameof(Candidate.Id), Assert.Single(user.Properties).Name);
        Assert.True(user.IsUnique);
        Assert.Equal(DeleteBehavior.NoAction, user.DeleteBehavior);

        IForeignKey location = Assert.Single(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Location));
        Assert.Equal(nameof(Candidate.LocationId), Assert.Single(location.Properties).Name);
        Assert.Equal(DeleteBehavior.NoAction, location.DeleteBehavior);

        IIndex index = Assert.Single(entity.GetIndexes());
        Assert.Equal("ix_candidate_location", index.GetDatabaseName());
        Assert.Equal(nameof(Candidate.LocationId), Assert.Single(index.Properties).Name);
    }

    /// <summary>
    /// Os seis <c>CHECK</c> da tabela <c>candidate</c>, incluindo <c>ck_candidate_quota_block</c>.
    /// </summary>
    [Fact]
    public void DeclaresTheCandidateCheckConstraints()
    {
        Dictionary<string, string> checks = GetEntity<Candidate>().GetCheckConstraints()
            .ToDictionary(check => check.Name!, check => check.Sql);

        Assert.Equal(6, checks.Count);
        Assert.Equal("btrim(full_name) <> ''", checks["ck_candidate_full_name"]);
        Assert.Equal("phone_number ~ '^[0-9]{9}$'", checks["ck_candidate_phone_number"]);
        Assert.Equal("max_distance_km BETWEEN 1 AND 500", checks["ck_candidate_max_distance_km"]);
        Assert.Equal("min_salary_expectation >= 0", checks["ck_candidate_min_salary_expectation"]);
        Assert.Equal("swipe_rights_remaining BETWEEN 0 AND 10", checks["ck_candidate_swipe_rights_remaining"]);
        Assert.Equal("next_swipe_available_at IS NULL OR swipe_rights_remaining = 0", checks["ck_candidate_quota_block"]);
    }

    /// <summary>
    /// A tabela <c>location</c>: colunas, precisão das coordenadas, <c>UNIQUE (name, district)</c> e <c>CHECK</c>.
    /// </summary>
    [Fact]
    public void MapsLocation()
    {
        IEntityType entity = GetEntity<Location>();
        var table = StoreObjectIdentifier.Table("location");

        Assert.Equal("location", entity.GetTableName());
        Assert.Equal(
            ["district", "id", "latitude", "longitude", "name"],
            entity.GetProperties().Select(property => property.GetColumnName(table)!).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("numeric(9,6)", entity.FindProperty(nameof(Location.Latitude))!.GetColumnType());
        Assert.Equal("numeric(9,6)", entity.FindProperty(nameof(Location.Longitude))!.GetColumnType());
        Assert.Equal(100, entity.FindProperty(nameof(Location.Name))!.GetMaxLength());
        Assert.Equal(50, entity.FindProperty(nameof(Location.District))!.GetMaxLength());

        IIndex unique = Assert.Single(entity.GetIndexes());
        Assert.True(unique.IsUnique);
        Assert.Equal("uq_location_name_district", unique.GetDatabaseName());
        Assert.Equal(["name", "district"], unique.Properties.Select(property => property.GetColumnName(table)!).ToArray());

        Dictionary<string, string> checks = entity.GetCheckConstraints().ToDictionary(check => check.Name!, check => check.Sql);
        Assert.Equal("latitude BETWEEN -90 AND 90", checks["ck_location_latitude"]);
        Assert.Equal("longitude BETWEEN -180 AND 180", checks["ck_location_longitude"]);
        Assert.Equal(4, checks.Count);
    }

    /// <summary>
    /// A tabela <c>candidate_link</c>: eliminação em cascata, posições e endereços únicos por Candidato e <c>CHECK</c>.
    /// </summary>
    [Fact]
    public void MapsCandidateLink()
    {
        IEntityType entity = GetEntity<CandidateLink>();
        var table = StoreObjectIdentifier.Table("candidate_link");

        Assert.Equal("candidate_link", entity.GetTableName());
        Assert.Equal(
            ["candidate_id", "id", "position", "url"],
            entity.GetProperties().Select(property => property.GetColumnName(table)!).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(500, entity.FindProperty(nameof(CandidateLink.Url))!.GetMaxLength());
        Assert.Equal("gen_random_uuid()", entity.FindProperty(nameof(CandidateLink.Id))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(CandidateLink.Id))!.ValueGenerated);

        IForeignKey candidate = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(Candidate), candidate.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, candidate.DeleteBehavior);

        Dictionary<string, string[]> indexes = entity.GetIndexes().ToDictionary(
            index => index.GetDatabaseName()!,
            index => index.Properties.Select(property => property.GetColumnName(table)!).ToArray());
        Assert.Equal(2, indexes.Count);
        Assert.Equal(["candidate_id", "position"], indexes["uq_candidate_link_position"]);
        Assert.Equal(["candidate_id", "url"], indexes["uq_candidate_link_url"]);
        Assert.All(entity.GetIndexes(), index => Assert.True(index.IsUnique));

        Dictionary<string, string> checks = entity.GetCheckConstraints().ToDictionary(check => check.Name!, check => check.Sql);
        Assert.Equal("position BETWEEN 1 AND 3", checks["ck_candidate_link_position"]);
        Assert.Equal("url ~* '^https?://'", checks["ck_candidate_link_url"]);
    }

    private static IEntityType GetEntity<TEntity>()
    {
        var builder = new DbContextOptionsBuilder<KatchDbContext>();
        builder.UseKatchPostgres("Host=localhost;Database=katch");

        using var context = new KatchDbContext(builder.Options);
        IModel model = context.GetService<IDesignTimeModel>().Model;
        return model.FindEntityType(typeof(TEntity))!;
    }
}
