// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katch.Backend.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAppUser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "app_user",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                user_type = table.Column<UserType>(type: "user_type", nullable: false),
                status = table.Column<AccountStatus>(type: "account_status", nullable: false, defaultValue: AccountStatus.Active),
                terms_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_app_user", x => x.id);
                table.CheckConstraint("ck_app_user_admin_active", "user_type <> 'ADMIN' OR status = 'ACTIVE'");
                table.CheckConstraint("ck_app_user_email", "email ~ '^[^@[:space:]]+@[^@[:space:]]+$'");
                table.CheckConstraint("ck_app_user_suspended", "status <> 'SUSPENDED' OR user_type = 'CANDIDATE'");
                table.CheckConstraint("ck_app_user_terms", "user_type = 'ADMIN' OR terms_accepted_at IS NOT NULL");
            });

        // Índice único sobre uma expressão (modelo de dados v02, secção 5.3): o modelo do EF Core não o representa,
        // por isso é criado por SQL e não consta do snapshot.
        migrationBuilder.Sql("CREATE UNIQUE INDEX uq_app_user_email ON app_user (lower(email));");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX uq_app_user_email;");

        migrationBuilder.DropTable(
            name: "app_user");
    }
}
