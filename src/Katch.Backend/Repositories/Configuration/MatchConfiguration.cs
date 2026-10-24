// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="Match"/> para a tabela <c>match</c> (modelo de dados v02, secções 5.2, 5.3, 6.13,
/// 7.1 e 7.3): chaves estrangeiras, unicidade Candidato–vaga, índices, restrições <c>CHECK</c> do estado e da conversa e
/// marca de concorrência <c>xmin</c>.
/// </summary>
/// <remarks>
/// As restrições <c>CHECK</c> e os filtros dos índices são SQL do PostgreSQL (nomes das colunas em snake_case e tipos
/// enumerados), por isso só são declarados quando o fornecedor é o Npgsql (ver <see cref="AppUserConfiguration"/>). A
/// marca de concorrência <c>xmin</c> também é específica do PostgreSQL.
/// </remarks>
public sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    // Expressões das restrições escritas em SQL do PostgreSQL, tal como no modelo de dados (secção 5.2).
    private const string DecisionCheck =
        "(status IN ('MATCHED', 'REJECTED_BY_RECRUITER')) = (recruiter_action_at IS NOT NULL) "
        + "AND (recruiter_action_at IS NOT NULL) = (decided_by IS NOT NULL) "
        + "AND (recruiter_action_at IS NULL OR profile_opened_at IS NOT NULL)";

    private const string ConversationCloseCheck =
        "coalesce(conversation_status = 'CLOSED', false) = (conversation_closed_at IS NOT NULL) "
        + "AND coalesce(conversation_status = 'CLOSED', false) = (conversation_close_reason IS NOT NULL)";

    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public MatchConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("match", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint(
                    "ck_match_declined",
                    "status <> 'DECLINED_BY_CANDIDATE' OR (profile_opened_at IS NULL AND recruiter_action_at IS NULL)");
                table.HasCheckConstraint("ck_match_decision", DecisionCheck);
                table.HasCheckConstraint(
                    "ck_match_conversation",
                    "(conversation_status IS NOT NULL) = (status = 'MATCHED')");
                table.HasCheckConstraint("ck_match_conversation_close", ConversationCloseCheck);
                table.HasCheckConstraint(
                    "ck_match_closed_by",
                    "(conversation_closed_by IS NOT NULL) = coalesce(conversation_close_reason = 'CLOSED_BY_PARTY', false)");
            }
        });

        builder.HasKey(match => match.Id);

        // O identificador é criado pela entidade; o predefinido gen_random_uuid() serve as inserções por SQL
        // (dados iniciais).
        builder.Property(match => match.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(match => match.CandidateId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(match => match.JobId)
            .OnDelete(DeleteBehavior.NoAction);

        // As colunas têm os nomes do modelo de dados (decided_by e conversation_closed_by), sem o sufixo _id.
        builder.Property(match => match.DecidedById)
            .HasColumnName("decided_by");

        builder.HasOne<Recruiter>()
            .WithMany()
            .HasForeignKey(match => match.DecidedById)
            .HasConstraintName("fk_match_recruiter_decided_by")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(match => match.ConversationClosedById)
            .HasColumnName("conversation_closed_by");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(match => match.ConversationClosedById)
            .HasConstraintName("fk_match_app_user_conversation_closed_by")
            .OnDelete(DeleteBehavior.NoAction);

        // Uma única resposta por par Candidato–vaga (RF019); a primeira coluna serve também a exclusão das vagas já
        // respondidas (RF014).
        builder.HasIndex(match => new { match.CandidateId, match.JobId })
            .IsUnique()
            .HasDatabaseName("uq_match_candidate_job");

        // Índices da secção 5.3: candidatos em espera e interesses por vaga, e indicadores do período (RF096). O filtro
        // é SQL do PostgreSQL (tipo match_status), por isso só é declarado com o Npgsql; no SQLite dos testes de
        // integração o índice existe sem filtro.
        builder.HasIndex(match => new { match.JobId, match.Status })
            .HasDatabaseName("ix_match_job_status");

        builder.HasIndex(match => new { match.Status, match.RecruiterActionAt })
            .HasDatabaseName("ix_match_matched");

        IndexBuilder<Match> interestAt = builder.HasIndex(match => match.CandidateActionAt)
            .HasDatabaseName("ix_match_interest_at");

        if (this.usesPostgres)
        {
            interestAt.HasFilter("status <> 'DECLINED_BY_CANDIDATE'");
        }

        builder.Property(match => match.Status)
            .IsRequired();

        builder.Property(match => match.CandidateActionAt)
            .HasDefaultValueSql("now()")
            .ValueGeneratedNever();

        if (this.usesPostgres)
        {
            // Marca de concorrência otimista: a coluna de sistema xmin, sem coluna própria (secção 7.3). Protege as
            // decisões do Recrutador e o encerramento da conversa (RF117, RF075).
            builder.Property(match => match.Version)
                .IsRowVersion();
        }
        else
        {
            builder.Ignore(match => match.Version);
        }
    }
}
