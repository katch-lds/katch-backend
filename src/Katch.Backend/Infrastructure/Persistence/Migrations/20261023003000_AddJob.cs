// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katch.Backend.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddJob : Migration
{
    private static readonly string[] CompanyStatusColumns = ["company_id", "status"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "job",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                company_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                min_salary = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                max_salary = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                location_id = table.Column<Guid>(type: "uuid", nullable: false),
                contract_type = table.Column<ContractType>(type: "contract_type", nullable: false),
                work_mode = table.Column<WorkMode>(type: "work_mode", nullable: false),
                is_urgent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                photo_paths = table.Column<List<string>>(type: "character varying(500)[]", nullable: false, defaultValueSql: "'{}'"),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                status = table.Column<JobStatus>(type: "job_status", nullable: false, defaultValue: JobStatus.Unpublished),
                first_published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                suspended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_job", x => x.id);
                table.CheckConstraint("ck_job_description", "btrim(description) <> ''");
                table.CheckConstraint("ck_job_min_salary", "min_salary > 0");
                table.CheckConstraint("ck_job_photo_paths", "cardinality(photo_paths) <= 5");
                table.CheckConstraint("ck_job_published", "status NOT IN ('PUBLISHED', 'SUSPENDED') OR first_published_at IS NOT NULL");
                table.CheckConstraint("ck_job_salary", "max_salary >= min_salary");
                table.CheckConstraint("ck_job_title", "btrim(title) <> ''");
                table.ForeignKey(
                    name: "fk_job_company_company_id",
                    column: x => x.company_id,
                    principalTable: "company",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "fk_job_location_location_id",
                    column: x => x.location_id,
                    principalTable: "location",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_job_company_status",
            table: "job",
            columns: CompanyStatusColumns,
            filter: "deleted_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_job_expiry",
            table: "job",
            column: "expires_at",
            filter: "status = 'PUBLISHED'");

        migrationBuilder.CreateIndex(
            name: "ix_job_location",
            table: "job",
            column: "location_id",
            filter: "deleted_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "job");
    }
}
