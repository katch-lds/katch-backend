// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katch.Backend.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCompany : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "company",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                recruiter_id = table.Column<Guid>(type: "uuid", nullable: false),
                company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                tax_id = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                industry = table.Column<Industry>(type: "industry", nullable: false),
                address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                location_id = table.Column<Guid>(type: "uuid", nullable: false),
                contact_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                contact_phone = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                responsible_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                logo_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                photo_paths = table.Column<List<string>>(type: "character varying(500)[]", nullable: false, defaultValueSql: "'{}'"),
                status = table.Column<CompanyStatus>(type: "company_status", nullable: false, defaultValue: CompanyStatus.Pending),
                status_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_company", x => x.id);
                table.CheckConstraint("ck_company_address", "btrim(address) <> ''");
                table.CheckConstraint("ck_company_company_name", "btrim(company_name) <> ''");
                table.CheckConstraint("ck_company_contact_email", "contact_email ~ '^[^@[:space:]]+@[^@[:space:]]+$'");
                table.CheckConstraint("ck_company_contact_phone", "contact_phone ~ '^[0-9]{9}$'");
                table.CheckConstraint("ck_company_photo_paths", "cardinality(photo_paths) <= 6");
                table.CheckConstraint("ck_company_reason", "(status = 'REJECTED') = (status_reason IS NOT NULL)");
                table.CheckConstraint("ck_company_reason_length", "status_reason IS NULL OR char_length(btrim(status_reason)) BETWEEN 10 AND 500");
                table.CheckConstraint("ck_company_responsible_name", "btrim(responsible_name) <> ''");
                table.CheckConstraint("ck_company_tax_id", "tax_id ~ '^[0-9]{9}$'");
                table.CheckConstraint("ck_company_website", "website ~* '^https?://'");
                table.ForeignKey(
                    name: "fk_company_location_location_id",
                    column: x => x.location_id,
                    principalTable: "location",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "fk_company_recruiter_recruiter_id",
                    column: x => x.recruiter_id,
                    principalTable: "recruiter",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_company_status",
            table: "company",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "uq_company_recruiter",
            table: "company",
            column: "recruiter_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_company_tax_id",
            table: "company",
            column: "tax_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "company");
    }
}
