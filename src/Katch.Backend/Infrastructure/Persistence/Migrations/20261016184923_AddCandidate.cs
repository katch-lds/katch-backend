// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katch.Backend.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCandidate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "location",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                district = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_location", x => x.id);
                table.CheckConstraint("ck_location_district", "btrim(district) <> ''");
                table.CheckConstraint("ck_location_latitude", "latitude BETWEEN -90 AND 90");
                table.CheckConstraint("ck_location_longitude", "longitude BETWEEN -180 AND 180");
                table.CheckConstraint("ck_location_name", "btrim(name) <> ''");
            });

        migrationBuilder.CreateTable(
            name: "candidate",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                phone_number = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                location_id = table.Column<Guid>(type: "uuid", nullable: false),
                desired_role = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                experience_summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                availability = table.Column<Availability>(type: "availability", nullable: true),
                profile_photo_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                cv_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                max_distance_km = table.Column<int>(type: "integer", nullable: true),
                min_salary_expectation = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                preferred_work_modes = table.Column<List<WorkMode>>(type: "work_mode[]", nullable: false, defaultValueSql: "'{}'"),
                preferred_contract_types = table.Column<List<ContractType>>(type: "contract_type[]", nullable: false, defaultValueSql: "'{}'"),
                swipe_rights_remaining = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)10),
                next_swipe_available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_candidate", x => x.id);
                table.CheckConstraint("ck_candidate_full_name", "btrim(full_name) <> ''");
                table.CheckConstraint("ck_candidate_max_distance_km", "max_distance_km BETWEEN 1 AND 500");
                table.CheckConstraint("ck_candidate_min_salary_expectation", "min_salary_expectation >= 0");
                table.CheckConstraint("ck_candidate_phone_number", "phone_number ~ '^[0-9]{9}$'");
                table.CheckConstraint("ck_candidate_quota_block", "next_swipe_available_at IS NULL OR swipe_rights_remaining = 0");
                table.CheckConstraint("ck_candidate_swipe_rights_remaining", "swipe_rights_remaining BETWEEN 0 AND 10");
                table.ForeignKey(
                    name: "fk_candidate_app_user_id",
                    column: x => x.id,
                    principalTable: "app_user",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "fk_candidate_location_location_id",
                    column: x => x.location_id,
                    principalTable: "location",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "candidate_link",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<short>(type: "smallint", nullable: false),
                url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_candidate_link", x => x.id);
                table.CheckConstraint("ck_candidate_link_position", "position BETWEEN 1 AND 3");
                table.CheckConstraint("ck_candidate_link_url", "url ~* '^https?://'");
                table.ForeignKey(
                    name: "fk_candidate_link_candidate_candidate_id",
                    column: x => x.candidate_id,
                    principalTable: "candidate",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_candidate_location",
            table: "candidate",
            column: "location_id");

        migrationBuilder.CreateIndex(
            name: "uq_candidate_link_position",
            table: "candidate_link",
            columns: ["candidate_id", "position"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_candidate_link_url",
            table: "candidate_link",
            columns: ["candidate_id", "url"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_location_name_district",
            table: "location",
            columns: ["name", "district"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "candidate_link");

        migrationBuilder.DropTable(
            name: "candidate");

        migrationBuilder.DropTable(
            name: "location");
    }
}
