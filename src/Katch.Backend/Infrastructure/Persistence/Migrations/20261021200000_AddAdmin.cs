// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katch.Backend.Infrastructure.Persistence.Migrations;

/// <summary>
/// Cria a tabela <c>admin</c> (modelo de dados v02, secções 5.2 e 6.2) e a conta de Administrador dos dados iniciais
/// (secção 9; RF083), com a palavra-passe lida da variável de ambiente <see cref="AdminAccountSeed.PasswordVariable"/>.
/// </summary>
public partial class AddAdmin : Migration
{
    private readonly Func<string> readPassword;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddAdmin"/> class. Usado pelo Entity Framework Core: a palavra-passe
    /// é lida da variável de ambiente quando a migration é aplicada ou quando o script é gerado.
    /// </summary>
    public AddAdmin()
        : this(AdminAccountSeed.ReadPasswordFromEnvironment)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AddAdmin"/> class com a origem da palavra-passe indicada (testes).
    /// </summary>
    /// <param name="readPassword">Função que devolve a palavra-passe da conta de Administrador.</param>
    internal AddAdmin(Func<string> readPassword)
    {
        this.readPassword = readPassword;
    }

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "admin",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_admin", x => x.id);
                table.ForeignKey(
                    name: "fk_admin_app_user_id",
                    column: x => x.id,
                    principalTable: "app_user",
                    principalColumn: "id");
            });

        // Dados iniciais (modelo de dados v02, secção 9): só o resumo da palavra-passe fica na base de dados.
        string passwordHash = AdminAccountSeed.HashPassword(this.readPassword());
        migrationBuilder.Sql(AdminAccountSeed.BuildInsertSql(passwordHash));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "admin");

        // Sem a tabela admin, as contas de Administrador deixam de ter especialização e são apagadas.
        migrationBuilder.Sql(AdminAccountSeed.DeleteSql);
    }
}
