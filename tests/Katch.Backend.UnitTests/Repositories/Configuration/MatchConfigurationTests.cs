// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Katch.Backend.UnitTests.Repositories.Configuration;

/// <summary>
/// Testes do mapeamento de <see cref="Match"/> contra a tabela <c>match</c> do modelo de dados v02 (secções 5.1, 5.2,
/// 5.3, 6.13, 7.1 e 7.3), verificado com o fornecedor Npgsql e sem abrir ligação. O <c>xmin</c> e os <c>CHECK</c> são
/// específicos do PostgreSQL e não são aplicados pelo SQLite dos testes de integração.
/// </summary>
public sealed class MatchConfigurationTests
{
    private static readonly StoreObjectIdentifier Table = StoreObjectIdentifier.Table("match");

    /// <summary>
    /// A tabela <c>match</c> tem as 12 colunas do modelo de dados, mais o <c>xmin</c>, sem <c>matched_at</c>,
    /// <c>profile_opened_by</c> nem <c>last_message_at</c> (DM-21 a DM-24) e sem datas de criação nem de alteração.
    /// </summary>
    [Fact]
    public void MapsExactlyTheColumnsOfTheDataModel()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("match", entity.GetTableName());
        Assert.Equal(
            [
                "candidate_action_at", "candidate_id", "conversation_close_reason", "conversation_closed_at",
                "conversation_closed_by", "conversation_status", "decided_by", "id", "job_id", "profile_opened_at",
                "recruiter_action_at", "status", "xmin",
            ],
            entity.GetProperties().Select(property => property.GetColumnName(Table)!).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Tipos das colunas do dicionário de dados (secção 6.13), com os quatro tipos enumerados do PostgreSQL.
    /// </summary>
    [Fact]
    public void MapsColumnTypes()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("uuid", ColumnType(entity, nameof(Match.Id)));
        Assert.Equal("uuid", ColumnType(entity, nameof(Match.CandidateId)));
        Assert.Equal("uuid", ColumnType(entity, nameof(Match.JobId)));
        Assert.Equal("match_status", ColumnType(entity, nameof(Match.Status)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Match.CandidateActionAt)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Match.ProfileOpenedAt)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Match.RecruiterActionAt)));
        Assert.Equal("uuid", ColumnType(entity, nameof(Match.DecidedById)));
        Assert.Equal("conversation_status", ColumnType(entity, nameof(Match.ConversationStatus)));
        Assert.Equal("timestamp with time zone", ColumnType(entity, nameof(Match.ConversationClosedAt)));
        Assert.Equal("uuid", ColumnType(entity, nameof(Match.ConversationClosedById)));
        Assert.Equal("conversation_close_reason", ColumnType(entity, nameof(Match.ConversationCloseReason)));
        Assert.Equal("xid", ColumnType(entity, nameof(Match.Version)));
    }

    /// <summary>
    /// Os nomes das colunas do autor da decisão e do autor do encerramento são os do modelo de dados, sem o sufixo
    /// <c>_id</c> da propriedade.
    /// </summary>
    [Fact]
    public void MapsTheAuthorColumnsWithTheNamesOfTheDataModel()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("decided_by", entity.FindProperty(nameof(Match.DecidedById))!.GetColumnName(Table));
        Assert.Equal("conversation_closed_by", entity.FindProperty(nameof(Match.ConversationClosedById))!.GetColumnName(Table));
    }

    /// <summary>
    /// Só as colunas da abertura do perfil, da decisão e da conversa admitem <c>NULL</c>; o estado, a data do interesse
    /// e as chaves são obrigatórios.
    /// </summary>
    [Fact]
    public void OnlyTheOptionalColumnsAreNullable()
    {
        Assert.Equal(
            [
                nameof(Match.ConversationCloseReason), nameof(Match.ConversationClosedAt), nameof(Match.ConversationClosedById),
                nameof(Match.ConversationStatus), nameof(Match.DecidedById), nameof(Match.ProfileOpenedAt),
                nameof(Match.RecruiterActionAt),
            ],
            GetEntity().GetProperties().Where(property => property.IsNullable).Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Predefinidos: <c>gen_random_uuid()</c> no identificador e <c>now()</c> na data do interesse; o estado não tem
    /// predefinido.
    /// </summary>
    [Fact]
    public void MapsDefaults()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("gen_random_uuid()", entity.FindProperty(nameof(Match.Id))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Match.Id))!.ValueGenerated);
        Assert.Equal("now()", entity.FindProperty(nameof(Match.CandidateActionAt))!.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Match.CandidateActionAt))!.ValueGenerated);
        Assert.Null(entity.FindProperty(nameof(Match.Status))!.GetDefaultValueSql());
        Assert.Null(entity.FindProperty(nameof(Match.Status))!.FindAnnotation(RelationalAnnotationNames.DefaultValue));
    }

    /// <summary>
    /// A marca de concorrência é a coluna de sistema <c>xmin</c> (<see cref="Match.Version"/>), sem coluna própria
    /// (secção 7.3).
    /// </summary>
    [Fact]
    public void VersionIsTheXminConcurrencyToken()
    {
        IProperty version = GetEntity().FindProperty(nameof(Match.Version))!;

        Assert.Equal("xmin", version.GetColumnName());
        Assert.Equal("xid", version.GetColumnType());
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
    }

    /// <summary>
    /// As quatro chaves estrangeiras: Candidato, vaga, Recrutador que decidiu e conta que encerrou a conversa, sem
    /// eliminação em cascata; as duas primeiras são obrigatórias.
    /// </summary>
    [Fact]
    public void MapsForeignKeys()
    {
        IEntityType entity = GetEntity();

        Assert.Equal("id", Assert.Single(entity.FindPrimaryKey()!.Properties).GetColumnName());
        Assert.Equal(4, entity.GetForeignKeys().Count());

        AssertForeignKey(entity, typeof(Candidate), nameof(Match.CandidateId), "fk_match_candidate_candidate_id", required: true);
        AssertForeignKey(entity, typeof(Job), nameof(Match.JobId), "fk_match_job_job_id", required: true);
        AssertForeignKey(entity, typeof(Recruiter), nameof(Match.DecidedById), "fk_match_recruiter_decided_by", required: false);
        AssertForeignKey(entity, typeof(AppUser), nameof(Match.ConversationClosedById), "fk_match_app_user_conversation_closed_by", required: false);
    }

    /// <summary>
    /// Os índices são exatamente os da secção 5.3, mais a unicidade Candidato–vaga: <c>ix_match_interest_at</c> só
    /// com as respostas que não são recusas da vaga.
    /// </summary>
    [Fact]
    public void MapsExactlyTheIndexesOfTheDataModel()
    {
        Dictionary<string, IIndex> indexes = GetEntity().GetIndexes().ToDictionary(index => index.GetDatabaseName()!);

        Assert.Equal(
            ["ix_match_interest_at", "ix_match_job_status", "ix_match_matched", "uq_match_candidate_job"],
            indexes.Keys.Order(StringComparer.Ordinal).ToArray());

        Assert.Equal(["candidate_id", "job_id"], Columns(indexes["uq_match_candidate_job"]));
        Assert.True(indexes["uq_match_candidate_job"].IsUnique);
        Assert.Null(indexes["uq_match_candidate_job"].GetFilter());

        Assert.Equal(["job_id", "status"], Columns(indexes["ix_match_job_status"]));
        Assert.False(indexes["ix_match_job_status"].IsUnique);

        Assert.Equal(["status", "recruiter_action_at"], Columns(indexes["ix_match_matched"]));
        Assert.False(indexes["ix_match_matched"].IsUnique);

        Assert.Equal(["candidate_action_at"], Columns(indexes["ix_match_interest_at"]));
        Assert.Equal("status <> 'DECLINED_BY_CANDIDATE'", indexes["ix_match_interest_at"].GetFilter());
        Assert.False(indexes["ix_match_interest_at"].IsUnique);
    }

    /// <summary>
    /// Os cinco <c>CHECK</c> da tabela <c>match</c> (secções 5.2 e 7.1), com as expressões do modelo de dados.
    /// </summary>
    [Fact]
    public void DeclaresTheMatchCheckConstraints()
    {
        Dictionary<string, string> checks = GetEntity().GetCheckConstraints()
            .ToDictionary(check => check.Name!, check => check.Sql);

        Assert.Equal(
            ["ck_match_closed_by", "ck_match_conversation", "ck_match_conversation_close", "ck_match_decision", "ck_match_declined"],
            checks.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(
            "status <> 'DECLINED_BY_CANDIDATE' OR (profile_opened_at IS NULL AND recruiter_action_at IS NULL)",
            checks["ck_match_declined"]);
        Assert.Equal(
            "(status IN ('MATCHED', 'REJECTED_BY_RECRUITER')) = (recruiter_action_at IS NOT NULL) "
            + "AND (recruiter_action_at IS NOT NULL) = (decided_by IS NOT NULL) "
            + "AND (recruiter_action_at IS NULL OR profile_opened_at IS NOT NULL)",
            checks["ck_match_decision"]);
        Assert.Equal(
            "(conversation_status IS NOT NULL) = (status = 'MATCHED')",
            checks["ck_match_conversation"]);
        Assert.Equal(
            "coalesce(conversation_status = 'CLOSED', false) = (conversation_closed_at IS NOT NULL) "
            + "AND coalesce(conversation_status = 'CLOSED', false) = (conversation_close_reason IS NOT NULL)",
            checks["ck_match_conversation_close"]);
        Assert.Equal(
            "(conversation_closed_by IS NOT NULL) = coalesce(conversation_close_reason = 'CLOSED_BY_PARTY', false)",
            checks["ck_match_closed_by"]);
    }

    /// <summary>
    /// A entidade <c>match</c> não tem filtro global: as respostas a vagas eliminadas não desaparecem das consultas
    /// (o filtro das vagas eliminadas é só da vaga, DM-09).
    /// </summary>
    [Fact]
    public void HasNoGlobalQueryFilter()
    {
        Assert.Empty(GetEntity().GetDeclaredQueryFilters());
    }

    private static void AssertForeignKey(IEntityType entity, Type principal, string property, string constraintName, bool required)
    {
        IForeignKey key = Assert.Single(entity.GetForeignKeys(), candidate => candidate.PrincipalEntityType.ClrType == principal);

        Assert.Equal(property, Assert.Single(key.Properties).Name);
        Assert.Equal(constraintName, key.GetConstraintName());
        Assert.Equal(required, key.IsRequired);
        Assert.False(key.IsUnique);
        Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior);
    }

    private static string[] Columns(IIndex index)
    {
        return [.. index.Properties.Select(property => property.GetColumnName(Table)!)];
    }

    private static string? ColumnType(IEntityType entity, string property)
    {
        return entity.FindProperty(property)!.GetColumnType();
    }

    private static IEntityType GetEntity()
    {
        var builder = new DbContextOptionsBuilder<KatchDbContext>();
        builder.UseKatchPostgres("Host=localhost;Database=katch");

        using var context = new KatchDbContext(builder.Options);
        return context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Match))!;
    }
}
