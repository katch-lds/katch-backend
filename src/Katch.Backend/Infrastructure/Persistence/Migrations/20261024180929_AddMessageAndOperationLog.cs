// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Katch.Backend.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMessageAndOperationLog : Migration
{
    private static readonly string[] MatchSentColumns = ["match_id", "sent_at"];

    private static readonly string[] EntityOccurredColumns = ["entity_id", "occurred_at"];

    private static readonly string[] OperationOccurredColumns = ["operation", "occurred_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "message",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                match_id = table.Column<Guid>(type: "uuid", nullable: false),
                sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_message", x => x.id);
                table.CheckConstraint("ck_message_content", "btrim(content) <> ''");
                table.CheckConstraint("ck_message_read", "read_at IS NULL OR read_at >= sent_at");
                table.ForeignKey(
                    name: "fk_message_app_user_sender_id",
                    column: x => x.sender_id,
                    principalTable: "app_user",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "fk_message_match_match_id",
                    column: x => x.match_id,
                    principalTable: "match",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "operation_log",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                user_id = table.Column<Guid>(type: "uuid", nullable: true),
                operation = table.Column<OperationType>(type: "operation_type", nullable: false),
                entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_operation_log", x => x.id);
                table.CheckConstraint("ck_operation_log_author", "user_id IS NOT NULL OR operation = 'JOB_CLOSED'");
                table.ForeignKey(
                    name: "fk_operation_log_app_user_user_id",
                    column: x => x.user_id,
                    principalTable: "app_user",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_message_match_sent",
            table: "message",
            columns: MatchSentColumns);

        migrationBuilder.CreateIndex(
            name: "ix_message_unread",
            table: "message",
            column: "match_id",
            filter: "read_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_operation_log_entity",
            table: "operation_log",
            columns: EntityOccurredColumns);

        migrationBuilder.CreateIndex(
            name: "ix_operation_log_occurred",
            table: "operation_log",
            columns: OperationOccurredColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "message");

        migrationBuilder.DropTable(
            name: "operation_log");
    }
}
