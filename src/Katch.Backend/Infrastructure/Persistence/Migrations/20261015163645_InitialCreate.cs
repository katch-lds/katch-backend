using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katch.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "ACTIVE,BLOCKED,SUSPENDED")
                .Annotation("Npgsql:Enum:availability", "IMMEDIATE,FIFTEEN_DAYS,ONE_MONTH,TO_BE_AGREED")
                .Annotation("Npgsql:Enum:company_status", "PENDING,APPROVED,REJECTED,SUSPENDED")
                .Annotation("Npgsql:Enum:contract_type", "PERMANENT,FIXED_TERM,INTERNSHIP,SERVICE_PROVISION")
                .Annotation("Npgsql:Enum:conversation_close_reason", "CLOSED_BY_PARTY,ACCOUNT_BLOCKED_OR_SUSPENDED,COMPANY_SUSPENDED")
                .Annotation("Npgsql:Enum:conversation_status", "OPEN,CLOSED")
                .Annotation("Npgsql:Enum:industry", "TECHNOLOGY,HEALTHCARE,RETAIL,HOSPITALITY,CONSTRUCTION,MANUFACTURING,EDUCATION,FINANCE,LOGISTICS,SERVICES,OTHER")
                .Annotation("Npgsql:Enum:job_status", "UNPUBLISHED,PUBLISHED,SUSPENDED,CLOSED")
                .Annotation("Npgsql:Enum:match_status", "DECLINED_BY_CANDIDATE,WAITING,MATCHED,REJECTED_BY_RECRUITER")
                .Annotation("Npgsql:Enum:operation_type", "COMPANY_SUBMITTED,COMPANY_UPDATED,COMPANY_APPROVED,COMPANY_REJECTED,COMPANY_SUSPENDED,COMPANY_REACTIVATED,JOB_CREATED,JOB_PUBLISHED,JOB_UPDATED,JOB_SUSPENDED,JOB_REPUBLISHED,JOB_CLOSED,JOB_DELETED,ACCOUNT_BLOCKED,ACCOUNT_REACTIVATED,ACCOUNT_SUSPENDED,SKILL_CREATED,SKILL_RENAMED,BENEFIT_CREATED,BENEFIT_RENAMED")
                .Annotation("Npgsql:Enum:user_type", "CANDIDATE,RECRUITER,ADMIN")
                .Annotation("Npgsql:Enum:work_mode", "ON_SITE,HYBRID,REMOTE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverte a criação dos tipos enumerados: o Npgsql gera DROP TYPE para cada anotação antiga.
            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:account_status", "ACTIVE,BLOCKED,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:availability", "IMMEDIATE,FIFTEEN_DAYS,ONE_MONTH,TO_BE_AGREED")
                .OldAnnotation("Npgsql:Enum:company_status", "PENDING,APPROVED,REJECTED,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:contract_type", "PERMANENT,FIXED_TERM,INTERNSHIP,SERVICE_PROVISION")
                .OldAnnotation("Npgsql:Enum:conversation_close_reason", "CLOSED_BY_PARTY,ACCOUNT_BLOCKED_OR_SUSPENDED,COMPANY_SUSPENDED")
                .OldAnnotation("Npgsql:Enum:conversation_status", "OPEN,CLOSED")
                .OldAnnotation("Npgsql:Enum:industry", "TECHNOLOGY,HEALTHCARE,RETAIL,HOSPITALITY,CONSTRUCTION,MANUFACTURING,EDUCATION,FINANCE,LOGISTICS,SERVICES,OTHER")
                .OldAnnotation("Npgsql:Enum:job_status", "UNPUBLISHED,PUBLISHED,SUSPENDED,CLOSED")
                .OldAnnotation("Npgsql:Enum:match_status", "DECLINED_BY_CANDIDATE,WAITING,MATCHED,REJECTED_BY_RECRUITER")
                .OldAnnotation("Npgsql:Enum:operation_type", "COMPANY_SUBMITTED,COMPANY_UPDATED,COMPANY_APPROVED,COMPANY_REJECTED,COMPANY_SUSPENDED,COMPANY_REACTIVATED,JOB_CREATED,JOB_PUBLISHED,JOB_UPDATED,JOB_SUSPENDED,JOB_REPUBLISHED,JOB_CLOSED,JOB_DELETED,ACCOUNT_BLOCKED,ACCOUNT_REACTIVATED,ACCOUNT_SUSPENDED,SKILL_CREATED,SKILL_RENAMED,BENEFIT_CREATED,BENEFIT_RENAMED")
                .OldAnnotation("Npgsql:Enum:user_type", "CANDIDATE,RECRUITER,ADMIN")
                .OldAnnotation("Npgsql:Enum:work_mode", "ON_SITE,HYBRID,REMOTE");
        }
    }
}
