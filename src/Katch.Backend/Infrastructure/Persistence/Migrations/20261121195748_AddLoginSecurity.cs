// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katch.Backend.Infrastructure.Persistence.Migrations;

/// <summary>
/// Acrescenta a <c>app_user</c> o contador de inícios de sessão falhados e o fim do bloqueio temporário (SEG-002) e a
/// versão das sessões, escrita em cada credencial (SEG-003). As contas existentes ficam com zero falhas, sem bloqueio e
/// na versão 0, por isso as credenciais emitidas antes desta migration deixam de ser aceites.
/// </summary>
public partial class AddLoginSecurity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "failed_login_count",
            table: "app_user",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "locked_until",
            table: "app_user",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "token_version",
            table: "app_user",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "failed_login_count",
            table: "app_user");

        migrationBuilder.DropColumn(
            name: "locked_until",
            table: "app_user");

        migrationBuilder.DropColumn(
            name: "token_version",
            table: "app_user");
    }
}
