// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="Message"/>: criação, validação do texto (P03) e marca de leitura (modelo de dados v02,
/// secções 6.14 e 7.1; modelo de classes do backend v02, secção 3.4).
/// </summary>
public sealed class MessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid MatchId = Guid.NewGuid();

    private static readonly Guid SenderId = Guid.NewGuid();

    /// <summary>
    /// A mensagem nova guarda o match, o remetente, o texto e a data de envio, e fica por ler.
    /// </summary>
    [Fact]
    public void SendCreatesAnUnreadMessage()
    {
        var message = Message.Send(MatchId, SenderId, "Olá, quando podemos falar?", Now);

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(MatchId, message.MatchId);
        Assert.Equal(SenderId, message.SenderId);
        Assert.Equal("Olá, quando podemos falar?", message.Content);
        Assert.Equal(Now, message.SentAt);
        Assert.Null(message.ReadAt);
    }

    /// <summary>
    /// Cada mensagem tem o seu identificador.
    /// </summary>
    [Fact]
    public void EachMessageHasItsOwnIdentifier()
    {
        Assert.NotEqual(
            Message.Send(MatchId, SenderId, "Olá", Now).Id,
            Message.Send(MatchId, SenderId, "Olá", Now).Id);
    }

    /// <summary>
    /// O match e o remetente são obrigatórios.
    /// </summary>
    [Fact]
    public void EmptyIdentifiersAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Message.Send(Guid.Empty, SenderId, "Olá", Now));
        Assert.Throws<ArgumentException>(() => Message.Send(MatchId, Guid.Empty, "Olá", Now));
    }

    /// <summary>
    /// O texto é obrigatório: nulo, vazio ou só com espaços e mudanças de linha é rejeitado.
    /// </summary>
    /// <param name="content">Texto inválido.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t\r\n  ")]
    public void BlankContentIsRejected(string content)
    {
        Assert.Throws<ArgumentException>(() => Message.Send(MatchId, SenderId, content, Now));
    }

    /// <summary>
    /// Um texto nulo é rejeitado.
    /// </summary>
    [Fact]
    public void NullContentIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Message.Send(MatchId, SenderId, null!, Now));
    }

    /// <summary>
    /// O limite é de 1000 caracteres (P03): 1000 é aceite e 1001 é rejeitado.
    /// </summary>
    [Fact]
    public void ContentIsLimitedTo1000Characters()
    {
        Message limit = Message.Send(MatchId, SenderId, new string('a', 1000), Now);

        Assert.Equal(1000, limit.Content.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => Message.Send(MatchId, SenderId, new string('a', 1001), Now));
    }

    /// <summary>
    /// O texto é guardado tal como foi enviado, sem remover espaços nem mudanças de linha.
    /// </summary>
    [Fact]
    public void ContentIsKeptUntouched()
    {
        const string content = "  Bom dia,\r\n\r\ntenho disponibilidade amanhã.  ";

        Assert.Equal(content, Message.Send(MatchId, SenderId, content, Now).Content);
    }

    /// <summary>
    /// O texto é confidencial (RNF017): nenhuma das exceções de validação o inclui na mensagem.
    /// </summary>
    [Fact]
    public void ExceptionsDoNotExposeTheContent()
    {
        string tooLong = "segredo-" + new string('x', 1000);

        ArgumentOutOfRangeException tooLongError = Assert.Throws<ArgumentOutOfRangeException>(
            () => Message.Send(MatchId, SenderId, tooLong, Now));
        ArgumentException emptyIdError = Assert.Throws<ArgumentException>(
            () => Message.Send(Guid.Empty, SenderId, "segredo", Now));

        Assert.DoesNotContain("segredo", tooLongError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo", emptyIdError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo", tooLongError.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("segredo", Message.Send(MatchId, SenderId, "segredo", Now).ToString()!, StringComparison.Ordinal);
    }

    /// <summary>
    /// A primeira leitura regista a data.
    /// </summary>
    [Fact]
    public void MarkReadRegistersTheFirstReading()
    {
        Message message = Message.Send(MatchId, SenderId, "Olá", Now);

        message.MarkRead(Now.AddMinutes(3));

        Assert.Equal(Now.AddMinutes(3), message.ReadAt);
    }

    /// <summary>
    /// Uma mensagem já lida não é alterada por nova leitura: mantém a data da primeira.
    /// </summary>
    [Fact]
    public void MarkReadOnAnAlreadyReadMessageChangesNothing()
    {
        Message message = Message.Send(MatchId, SenderId, "Olá", Now);
        message.MarkRead(Now.AddMinutes(3));

        message.MarkRead(Now.AddHours(1));

        Assert.Equal(Now.AddMinutes(3), message.ReadAt);
    }

    /// <summary>
    /// A leitura no próprio instante do envio é aceite, porque o <c>CHECK</c> exige <c>read_at &gt;= sent_at</c>.
    /// </summary>
    [Fact]
    public void MarkReadAtTheInstantOfSendingIsAccepted()
    {
        Message message = Message.Send(MatchId, SenderId, "Olá", Now);

        message.MarkRead(Now);

        Assert.Equal(Now, message.ReadAt);
    }

    /// <summary>
    /// Uma leitura anterior ao envio é rejeitada e a mensagem continua por ler (<c>ck_message_read</c>).
    /// </summary>
    [Fact]
    public void MarkReadBeforeSendingIsRejected()
    {
        Message message = Message.Send(MatchId, SenderId, "Olá", Now);

        Assert.Throws<ArgumentException>(() => message.MarkRead(Now.AddSeconds(-1)));

        Assert.Null(message.ReadAt);
    }
}
