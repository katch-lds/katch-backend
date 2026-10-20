// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="Location"/> para a tabela <c>location</c> (modelo de dados v02, secções 5.2 e 6.5).
/// </summary>
/// <remarks>
/// As restrições escritas em SQL do PostgreSQL só são declaradas quando o fornecedor é o Npgsql (ver
/// <see cref="AppUserConfiguration"/>).
/// </remarks>
public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocationConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public LocationConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("location", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint("ck_location_name", "btrim(name) <> ''");
                table.HasCheckConstraint("ck_location_district", "btrim(district) <> ''");
                table.HasCheckConstraint("ck_location_latitude", "latitude BETWEEN -90 AND 90");
                table.HasCheckConstraint("ck_location_longitude", "longitude BETWEEN -180 AND 180");
            }
        });

        builder.HasKey(location => location.Id);

        builder.Property(location => location.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(location => location.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(location => location.District)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(location => location.Latitude)
            .HasPrecision(9, 6);

        builder.Property(location => location.Longitude)
            .HasPrecision(9, 6);

        builder.HasIndex(location => new { location.Name, location.District })
            .IsUnique()
            .HasDatabaseName("uq_location_name_district");
    }
}
