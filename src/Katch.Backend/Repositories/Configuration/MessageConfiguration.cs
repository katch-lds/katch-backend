// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="Message"/> para a tabela <c>message</c> (modelo de dados v02, secções 5.2, 5.3,
/// 6.14 e 7.1): chaves estrangeiras para o match e para o remetente, texto até 1000 caracteres, restrições
/// <c>CHECK</c> do texto e da leitura e índices da conversa e das mensagens por ler.
/// </summary>
/// <remarks>
/// As restrições <c>CHECK</c> e os filtros dos índices são SQL do PostgreSQL, por isso só são declarados quando o
/// fornecedor é o Npgsql (ver <see cref="AppUserConfiguration"/>). O <c>CHECK</c> do texto não tem nome no modelo de
/// dados (o PostgreSQL daria <c>message_content_check</c>); aqui chama-se <c>ck_message_content</c>, como as restantes.
/// </remarks>
public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    private readonly bool usesPostgres;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageConfiguration"/> class.
    /// </summary>
    /// <param name="usesPostgres">Indica se o fornecedor da base de dados é o PostgreSQL.</param>
    public MessageConfiguration(bool usesPostgres)
    {
        this.usesPostgres = usesPostgres;
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("message", table =>
        {
            if (this.usesPostgres)
            {
                table.HasCheckConstraint("ck_message_content", "btrim(content) <> ''");
                table.HasCheckConstraint("ck_message_read", "read_at IS NULL OR read_at >= sent_at");
            }
        });

        builder.HasKey(message => message.Id);

        // O identificador é criado pela entidade; o predefinido gen_random_uuid() serve as inserções por SQL.
        builder.Property(message => message.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        builder.HasOne<Match>()
            .WithMany()
            .HasForeignKey(message => message.MatchId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(message => message.SenderId)
            .HasConstraintName("fk_message_app_user_sender_id")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(message => message.Content)
            .HasMaxLength(Message.MaxContentLength)
            .IsRequired();

        builder.Property(message => message.SentAt)
            .HasDefaultValueSql("now()")
            .ValueGeneratedNever();

        // Histórico da conversa por ordem de envio.
        builder.HasIndex(message => new { message.MatchId, message.SentAt })
            .HasDatabaseName("ix_message_match_sent");

        // Mensagens por ler de cada conversa. O filtro é SQL do PostgreSQL, por isso só é declarado com o
        // Npgsql; no SQLite dos testes de integração o índice existe sem filtro.
        IndexBuilder<Message> unread = builder.HasIndex(message => message.MatchId)
            .HasDatabaseName("ix_message_unread");

        if (this.usesPostgres)
        {
            unread.HasFilter("read_at IS NULL");
        }
    }
}
