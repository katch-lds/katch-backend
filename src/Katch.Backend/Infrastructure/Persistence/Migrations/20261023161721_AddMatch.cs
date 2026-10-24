// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katch.Backend.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMatch : Migration
{
    private static readonly string[] JobStatusColumns = ["job_id", "status"];

    private static readonly string[] StatusRecruiterActionColumns = ["status", "recruiter_action_at"];

    private static readonly string[] CandidateJobColumns = ["candidate_id", "job_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "match",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<MatchStatus>(type: "match_status", nullable: false),
                candidate_action_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                profile_opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                recruiter_action_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                conversation_status = table.Column<ConversationStatus>(type: "conversation_status", nullable: true),
                conversation_closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                conversation_closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                conversation_close_reason = table.Column<ConversationCloseReason>(type: "conversation_close_reason", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_match", x => x.id);
                table.CheckConstraint("ck_match_closed_by", "(conversation_closed_by IS NOT NULL) = coalesce(conversation_close_reason = 'CLOSED_BY_PARTY', false)");
                table.CheckConstraint("ck_match_conversation", "(conversation_status IS NOT NULL) = (status = 'MATCHED')");
                table.CheckConstraint("ck_match_conversation_close", "coalesce(conversation_status = 'CLOSED', false) = (conversation_closed_at IS NOT NULL) AND coalesce(conversation_status = 'CLOSED', false) = (conversation_close_reason IS NOT NULL)");
                table.CheckConstraint("ck_match_decision", "(status IN ('MATCHED', 'REJECTED_BY_RECRUITER')) = (recruiter_action_at IS NOT NULL) AND (recruiter_action_at IS NOT NULL) = (decided_by IS NOT NULL) AND (recruiter_action_at IS NULL OR profile_opened_at IS NOT NULL)");
                table.CheckConstraint("ck_match_declined", "status <> 'DECLINED_BY_CANDIDATE' OR (profile_opened_at IS NULL AND recruiter_action_at IS NULL)");
                table.ForeignKey(
                    name: "fk_match_app_user_conversation_closed_by",
                    column: x => x.conversation_closed_by,
                    principalTable: "app_user",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "fk_match_candidate_candidate_id",
                    column: x => x.candidate_id,
                    principalTable: "candidate",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "fk_match_job_job_id",
                    column: x => x.job_id,
                    principalTable: "job",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "fk_match_recruiter_decided_by",
                    column: x => x.decided_by,
                    principalTable: "recruiter",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_match_interest_at",
            table: "match",
            column: "candidate_action_at",
            filter: "status <> 'DECLINED_BY_CANDIDATE'");

        migrationBuilder.CreateIndex(
            name: "ix_match_job_status",
            table: "match",
            columns: JobStatusColumns);

        migrationBuilder.CreateIndex(
            name: "ix_match_matched",
            table: "match",
            columns: StatusRecruiterActionColumns);

        migrationBuilder.CreateIndex(
            name: "uq_match_candidate_job",
            table: "match",
            columns: CandidateJobColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "match");
    }
}
