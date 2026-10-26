// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katch.Backend.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSkillsAndBenefits : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "benefit",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_benefit", x => x.id);
                table.CheckConstraint("ck_benefit_name", "btrim(name) <> ''");
            });

        migrationBuilder.CreateTable(
            name: "candidate_custom_skill",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_candidate_custom_skill", x => x.id);
                table.CheckConstraint("ck_candidate_custom_skill_label", "char_length(label) BETWEEN 2 AND 40 AND btrim(label) <> '' AND label ~ '^[[:alnum:] +#.-]+$'");
                table.ForeignKey(
                    name: "fk_candidate_custom_skill_candidate_candidate_id",
                    column: x => x.candidate_id,
                    principalTable: "candidate",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "skill",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_skill", x => x.id);
                table.CheckConstraint("ck_skill_name", "btrim(name) <> ''");
            });

        migrationBuilder.CreateTable(
            name: "job_benefit",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                benefit_id = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_job_benefit", x => new { x.job_id, x.benefit_id });
                table.ForeignKey(
                    name: "fk_job_benefit_benefit_benefit_id",
                    column: x => x.benefit_id,
                    principalTable: "benefit",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "fk_job_benefit_job_job_id",
                    column: x => x.job_id,
                    principalTable: "job",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "candidate_skill",
            columns: table => new
            {
                candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                skill_id = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_candidate_skill", x => new { x.candidate_id, x.skill_id });
                table.ForeignKey(
                    name: "fk_candidate_skill_candidate_candidate_id",
                    column: x => x.candidate_id,
                    principalTable: "candidate",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_candidate_skill_skill_skill_id",
                    column: x => x.skill_id,
                    principalTable: "skill",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "job_skill",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                skill_id = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_job_skill", x => new { x.job_id, x.skill_id });
                table.ForeignKey(
                    name: "fk_job_skill_job_job_id",
                    column: x => x.job_id,
                    principalTable: "job",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_job_skill_skill_skill_id",
                    column: x => x.skill_id,
                    principalTable: "skill",
                    principalColumn: "id");
            });

        // Índices únicos sobre expressões (modelo de dados v02, secção 5.3): o modelo do EF Core não os representa, por
        // isso são criados por SQL e não constam do snapshot.
        migrationBuilder.Sql("CREATE UNIQUE INDEX uq_skill_name ON skill (lower(name));");
        migrationBuilder.Sql("CREATE UNIQUE INDEX uq_benefit_name ON benefit (lower(name));");
        migrationBuilder.Sql("CREATE UNIQUE INDEX uq_candidate_custom_skill ON candidate_custom_skill (candidate_id, lower(label));");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX uq_candidate_custom_skill;");
        migrationBuilder.Sql("DROP INDEX uq_benefit_name;");
        migrationBuilder.Sql("DROP INDEX uq_skill_name;");

        migrationBuilder.DropTable(
            name: "candidate_custom_skill");

        migrationBuilder.DropTable(
            name: "candidate_skill");

        migrationBuilder.DropTable(
            name: "job_benefit");

        migrationBuilder.DropTable(
            name: "job_skill");

        migrationBuilder.DropTable(
            name: "benefit");

        migrationBuilder.DropTable(
            name: "skill");
    }
}
