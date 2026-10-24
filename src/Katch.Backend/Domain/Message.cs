// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain;

/// <summary>
/// Mensagem trocada na conversa de um match (tabela <c>message</c>): remetente, texto, data de envio e data de leitura.
/// </summary>
/// <remarks>
/// A mensagem não se edita nem se elimina (modelo de classes do backend v02, secção 3.4): a única alteração possível é
/// a marca de leitura (<see cref="MarkRead"/>). As regras que dependem só desta linha — texto não vazio e com no máximo
/// 1000 caracteres (P03) e leitura nunca anterior ao envio — correspondem aos <c>CHECK</c> da tabela. Que o remetente
/// pertence ao match e que a conversa está aberta (<see cref="Match.CanSendMessages"/>) dependem de outras linhas e são
/// verificados pelos serviços. O texto é confidencial (RNF017): nunca consta de mensagens de exceção nem de registos.
/// </remarks>
public sealed class Message
{
    /// <summary>
    /// Número máximo de caracteres do texto (P03).
    /// </summary>
    public const int MaxContentLength = 1000;

    private Message(Guid matchId, Guid senderId, string content, DateTimeOffset now)
    {
        if (matchId == Guid.Empty)
        {
            throw new ArgumentException("O identificador do match é obrigatório.", nameof(matchId));
        }

        if (senderId == Guid.Empty)
        {
            throw new ArgumentException("O identificador do remetente é obrigatório.", nameof(senderId));
        }

        if (content is null)
        {
            throw new ArgumentNullException(nameof(content), "O texto da mensagem é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("O texto da mensagem não pode estar vazio.", nameof(content));
        }

        if (content.Length > MaxContentLength)
        {
            // Só o tamanho é indicado: o texto é confidencial (RNF017).
            throw new ArgumentOutOfRangeException(
                nameof(content),
                content.Length,
                $"O texto da mensagem não pode ter mais de {MaxContentLength} caracteres (P03).");
        }

        this.Id = Guid.NewGuid();
        this.MatchId = matchId;
        this.SenderId = senderId;
        this.Content = content;
        this.SentAt = now;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Message"/> class. Usado pelo Entity Framework Core.
    /// </summary>
    private Message()
    {
        this.Content = string.Empty;
    }

    /// <summary>
    /// Gets the identifier of the message (<c>id</c>).
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the identifier of the match, which is also the identifier of the conversation (<c>match_id</c>).
    /// </summary>
    public Guid MatchId { get; private set; }

    /// <summary>
    /// Gets the identifier of the account that sent the message (<c>sender_id</c>).
    /// </summary>
    public Guid SenderId { get; private set; }

    /// <summary>
    /// Gets the text of the message (<c>content</c>), kept exactly as it was sent. It is confidential (RNF017).
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// Gets the instant in which the message was sent (<c>sent_at</c>).
    /// </summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>
    /// Gets the instant in which the message was read by the other party (<c>read_at</c>); empty while unread.
    /// </summary>
    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>
    /// Cria uma mensagem nova, ainda por ler, com o texto tal como foi enviado (sem remover espaços). Que o remetente
    /// pertence ao match e que a conversa está aberta é verificado pelo serviço (RF069, RF070).
    /// </summary>
    /// <param name="matchId">Identificador do match (conversa).</param>
    /// <param name="senderId">Identificador da conta do remetente.</param>
    /// <param name="content">Texto da mensagem, com pelo menos um carácter visível e no máximo 1000 caracteres (P03).</param>
    /// <param name="now">Instante do envio, em UTC.</param>
    /// <returns>A nova mensagem.</returns>
    /// <exception cref="ArgumentException">
    /// Se algum dos identificadores estiver vazio, ou se o texto não existir ou tiver só espaços.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Se o texto tiver mais de 1000 caracteres.</exception>
    public static Message Send(Guid matchId, Guid senderId, string content, DateTimeOffset now)
    {
        return new Message(matchId, senderId, content, now);
    }

    /// <summary>
    /// Marca a mensagem como lida. Na primeira chamada preenche <see cref="ReadAt"/>; se já estiver lida, não altera
    /// nada.
    /// </summary>
    /// <param name="now">Instante da leitura, em UTC.</param>
    /// <exception cref="ArgumentException">Se o instante for anterior ao envio, o que o <c>CHECK</c> não admite.</exception>
    public void MarkRead(DateTimeOffset now)
    {
        if (this.ReadAt is not null)
        {
            return;
        }

        if (now < this.SentAt)
        {
            throw new ArgumentException("A leitura não pode ser anterior ao envio da mensagem.", nameof(now));
        }

        this.ReadAt = now;
    }
}
