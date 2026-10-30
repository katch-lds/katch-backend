// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes do <see cref="ConversationService"/> (documentação da API v02, secções 6.8.1 a 6.8.4), com repositórios em
/// memória.
/// </summary>
public sealed class ConversationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid candidateId = Guid.NewGuid();
    private readonly Guid recruiterId = Guid.NewGuid();

    /// <summary>
    /// A lista põe primeiro as conversas sem mensagens, pela data do match, e depois as restantes pela última mensagem, e
    /// traduz o estado, o motivo e o autor.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListOrdersTheConversationsWithoutMessagesFirstAndMapsTheClosure()
    {
        ConversationView emptyOld = View(this.NewMatch(Now.AddDays(-9)), lastMessageAt: null);
        ConversationView emptyNew = View(this.NewMatch(Now.AddDays(-2)), lastMessageAt: null);
        ConversationView recent = View(this.NewMatch(Now.AddDays(-8)), Now.AddHours(-1), unread: 3);
        Match closed = this.NewMatch(Now.AddDays(-7));
        closed.CloseConversation(this.candidateId, ConversationCloseReason.ClosedByParty, Now.AddDays(-6));
        ConversationView old = View(closed, Now.AddHours(-9));
        var (service, _, _, _) = this.Create(views: [old, recent, emptyOld, emptyNew]);

        List<ConversationSummaryDto> result = await service.ListAsync(this.candidateId, TestContext.Current.CancellationToken);

        Assert.Equal(
            [emptyNew.Match.Id, emptyOld.Match.Id, recent.Match.Id, old.Match.Id],
            result.Select(dto => dto.MatchId).ToList());
        Assert.Equal(3, result[2].UnreadCount);
        Assert.Equal(ConversationStatus.Open, result[2].Status);
        Assert.Null(result[2].CloseReason);
        Assert.Null(result[2].ClosedBy);
        Assert.Null(result[0].LastMessageAt);
        Assert.Equal(ConversationStatus.Closed, result[3].Status);
        Assert.Equal(ConversationCloseReason.ClosedByParty, result[3].CloseReason);
        Assert.Equal(this.candidateId, result[3].ClosedBy);
        Assert.Equal("Rui Matos", result[3].CounterpartName);
    }

    /// <summary>
    /// Abrir a conversa marca como lidas as mensagens da outra parte com o instante do relógio e depois lê, com o filtro
    /// <c>after</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenMarksTheOtherPartysMessagesAsReadBeforeListingThem()
    {
        Match match = this.NewMatch(Now.AddDays(-1));
        var (service, _, messages, _) = this.Create(match);
        DateTimeOffset after = Now.AddMinutes(-5);

        await service.OpenAsync(this.candidateId, match.Id, after, TestContext.Current.CancellationToken);

        Assert.Equal(["mark", "list"], messages.Calls);
        Assert.Equal((match.Id, this.candidateId, Now), messages.MarkedWith);
        Assert.Equal(after, messages.ListedAfter);
    }

    /// <summary>
    /// O Recrutador da Empresa da vaga também é parte da conversa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TheRecruiterOfTheCompanyIsAParty()
    {
        Match match = this.NewMatch(Now.AddDays(-1));
        var (service, _, messages, _) = this.Create(match);

        await service.OpenAsync(this.recruiterId, match.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(this.recruiterId, messages.MarkedWith?.Reader);
    }

    /// <summary>
    /// A ordem das verificações é a existência (ERR-20), a parte do match (ERR-08) e o match confirmado (ERR-37), em todas
    /// as operações.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EveryOperationChecksExistenceThenPartyThenConfirmedMatch()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Match waiting = Match.ExpressInterest(this.candidateId, Guid.NewGuid(), Now);
        var (service, _, messages, unitOfWork) = this.Create(waiting);
        var request = new SendMessageRequest("Olá");

        await Assert.ThrowsAsync<ElementNotFoundException>(() => service.OpenAsync(this.candidateId, Guid.NewGuid(), null, token));
        await Assert.ThrowsAsync<ElementNotFoundException>(() => service.SendAsync(this.candidateId, Guid.NewGuid(), request, token));
        await Assert.ThrowsAsync<ElementNotFoundException>(() => service.CloseAsync(this.candidateId, Guid.NewGuid(), token));

        Guid stranger = Guid.NewGuid();
        await Assert.ThrowsAsync<ElementForbiddenException>(() => service.OpenAsync(stranger, waiting.Id, null, token));
        await Assert.ThrowsAsync<ElementForbiddenException>(() => service.SendAsync(stranger, waiting.Id, request, token));
        await Assert.ThrowsAsync<ElementForbiddenException>(() => service.CloseAsync(stranger, waiting.Id, token));

        AssertConflict("ERR-37", await Assert.ThrowsAsync<OperationConflictException>(() => service.OpenAsync(this.candidateId, waiting.Id, null, token)));
        AssertConflict("ERR-37", await Assert.ThrowsAsync<OperationConflictException>(() => service.SendAsync(this.candidateId, waiting.Id, request, token)));
        AssertConflict("ERR-37", await Assert.ThrowsAsync<OperationConflictException>(() => service.CloseAsync(this.candidateId, waiting.Id, token)));

        Assert.Empty(messages.Calls);
        Assert.Equal(0, unitOfWork.Saves);
    }

    /// <summary>
    /// O envio grava a mensagem tal como enviada, com o remetente da credencial e o instante do relógio, numa só gravação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SendSavesTheMessageAsSentWithTheSenderAndTheClockInstant()
    {
        Match match = this.NewMatch(Now.AddDays(-1));
        var (service, _, messages, unitOfWork) = this.Create(match);

        MessageDto dto = await service.SendAsync(this.candidateId, match.Id, new SendMessageRequest("  Olá  "), TestContext.Current.CancellationToken);

        Message saved = Assert.Single(messages.Added);
        Assert.Equal("  Olá  ", saved.Content);
        Assert.Equal(this.candidateId, saved.SenderId);
        Assert.Equal(Now, saved.SentAt);
        Assert.Equal(new MessageDto(saved.Id, this.candidateId, "  Olá  ", Now, null), dto);
        Assert.Equal(1, unitOfWork.Saves);
    }

    /// <summary>
    /// Um texto vazio, só de espaços, em falta ou com mais de 1000 caracteres dá ERR-10 no campo <c>content</c>, e nada é
    /// gravado; o texto nunca entra na exceção (RNF017).
    /// </summary>
    /// <param name="content">Texto enviado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("TOO_LONG")]
    public async Task SendRejectsAnInvalidContentWithoutLeakingIt(string? content)
    {
        Match match = this.NewMatch(Now.AddDays(-1));
        var (service, _, messages, unitOfWork) = this.Create(match);
        string? text = content == "TOO_LONG" ? new string('x', 1001) : content;

        ValidationFailedException error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => service.SendAsync(this.candidateId, match.Id, new SendMessageRequest(text), TestContext.Current.CancellationToken));

        Assert.Equal("content", Assert.Single(error.Fields).Field);
        Assert.DoesNotContain("xxx", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(messages.Added);
        Assert.Equal(0, unitOfWork.Saves);
    }

    /// <summary>
    /// Os limites de 1 e de 1000 caracteres são aceites.
    /// </summary>
    /// <param name="length">Número de caracteres.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task SendAcceptsTheLimitLengths(int length)
    {
        Match match = this.NewMatch(Now.AddDays(-1));
        var (service, _, messages, _) = this.Create(match);

        await service.SendAsync(this.candidateId, match.Id, new SendMessageRequest(new string('a', length)), TestContext.Current.CancellationToken);

        Assert.Equal(length, Assert.Single(messages.Added).Content.Length);
    }

    /// <summary>
    /// Numa conversa em modo apenas de consulta o ERR-36 vem antes da validação do texto, e nada é gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SendOnAClosedConversationGetsErr36BeforeTheContentIsValidated()
    {
        Match match = this.NewMatch(Now.AddDays(-2));
        match.CloseConversation(null, ConversationCloseReason.CompanySuspended, Now.AddDays(-1));
        var (service, _, messages, unitOfWork) = this.Create(match);

        OperationConflictException error = await Assert.ThrowsAsync<OperationConflictException>(
            () => service.SendAsync(this.candidateId, match.Id, new SendMessageRequest(string.Empty), TestContext.Current.CancellationToken));

        AssertConflict("ERR-36", error);
        Assert.Empty(messages.Added);
        Assert.Equal(0, unitOfWork.Saves);
    }

    /// <summary>
    /// Encerrar regista o motivo da parte, o autor e o instante do relógio, numa só gravação.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CloseRecordsTheReasonTheAuthorAndTheClockInstant()
    {
        Match match = this.NewMatch(Now.AddDays(-1));
        var (service, _, _, unitOfWork) = this.Create(match);

        await service.CloseAsync(this.recruiterId, match.Id, TestContext.Current.CancellationToken);

        Assert.Equal(ConversationStatus.Closed, match.ConversationStatus);
        Assert.Equal(ConversationCloseReason.ClosedByParty, match.ConversationCloseReason);
        Assert.Equal(this.recruiterId, match.ConversationClosedById);
        Assert.Equal(Now, match.ConversationClosedAt);
        Assert.Equal(1, unitOfWork.Saves);
    }

    /// <summary>
    /// Encerrar uma conversa já encerrada dá ERR-36 e não grava nada.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CloseOnAnAlreadyClosedConversationGetsErr36()
    {
        Match match = this.NewMatch(Now.AddDays(-2));
        match.CloseConversation(null, ConversationCloseReason.AccountBlockedOrSuspended, Now.AddDays(-1));
        var (service, _, _, unitOfWork) = this.Create(match);

        OperationConflictException error = await Assert.ThrowsAsync<OperationConflictException>(
            () => service.CloseAsync(this.candidateId, match.Id, TestContext.Current.CancellationToken));

        AssertConflict("ERR-36", error);
        Assert.Equal(0, unitOfWork.Saves);
        Assert.Equal(ConversationCloseReason.AccountBlockedOrSuspended, match.ConversationCloseReason);
    }

    /// <summary>
    /// Perder a concorrência (<c>xmin</c>) com um bloqueio ou uma suspensão simultâneos dá ERR-36 e não ERR-41.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LosingTheConcurrencyOnCloseGetsErr36AndNotErr41()
    {
        Match match = this.NewMatch(Now.AddDays(-1));
        var (service, _, _, unitOfWork) = this.Create(match);
        unitOfWork.FailWith = new ConcurrencyConflictException(new InvalidOperationException("xmin"));

        OperationConflictException error = await Assert.ThrowsAsync<OperationConflictException>(
            () => service.CloseAsync(this.candidateId, match.Id, TestContext.Current.CancellationToken));

        AssertConflict("ERR-36", error);
    }

    private static void AssertConflict(string code, OperationConflictException error)
    {
        Assert.Equal(code, error.Error.Code);
    }

    private static ConversationView View(Match match, DateTimeOffset? lastMessageAt, int unread = 0)
    {
        return new ConversationView(match, "Rui Matos", "Programador Backend .NET", "Lumen Software, Lda.", lastMessageAt, unread);
    }

    private Match NewMatch(DateTimeOffset matchedAt)
    {
        var match = Match.ExpressInterest(this.candidateId, Guid.NewGuid(), matchedAt.AddHours(-2));
        match.OpenProfile(matchedAt.AddHours(-1));
        match.Accept(this.recruiterId, matchedAt);
        return match;
    }

    private (ConversationService Service, FakeMatches Matches, FakeMessages Messages, FakeUnitOfWork UnitOfWork) Create(
        Match? match = null,
        List<ConversationView>? views = null)
    {
        var matches = new FakeMatches(match, this.recruiterId, views ?? []);
        var messages = new FakeMessages();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ConversationService(matches, messages, unitOfWork, new FixedClock(Now));
        return (service, matches, messages, unitOfWork);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now)
        {
            this.UtcNow = now;
        }

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public Exception? FailWith { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (this.FailWith is not null)
            {
                throw this.FailWith;
            }

            this.Saves++;
            return Task.FromResult(1);
        }

        public void DiscardChanges()
        {
            throw new NotSupportedException();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeMessages : IMessageRepository
    {
        public List<string> Calls { get; } = [];

        public List<Message> Added { get; } = [];

        public (Guid MatchId, Guid Reader, DateTimeOffset Now)? MarkedWith { get; private set; }

        public DateTimeOffset? ListedAfter { get; private set; }

        public Task AddAsync(Message message, CancellationToken cancellationToken = default)
        {
            this.Added.Add(message);
            return Task.CompletedTask;
        }

        public Task<List<Message>> ListByMatchAsync(Guid matchId, DateTimeOffset? after, CancellationToken cancellationToken = default)
        {
            this.Calls.Add("list");
            this.ListedAfter = after;
            return Task.FromResult(new List<Message>());
        }

        public Task<int> MarkReadAsync(Guid matchId, Guid readerId, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            this.Calls.Add("mark");
            this.MarkedWith = (matchId, readerId, now);
            return Task.FromResult(0);
        }
    }

    private sealed class FakeMatches : IMatchRepository
    {
        private readonly Match? match;
        private readonly Guid recruiterId;
        private readonly List<ConversationView> views;

        public FakeMatches(Match? match, Guid recruiterId, List<ConversationView> views)
        {
            this.match = match;
            this.recruiterId = recruiterId;
            this.views = views;
        }

        public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.match is not null && this.match.Id == id ? this.match : null);
        }

        public Task<Guid?> GetRecruiterIdAsync(Guid matchId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Guid?>(this.recruiterId);
        }

        public Task<List<ConversationView>> ListConversationsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.views.ToList());
        }

        public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Match match, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> HasInterestFromCandidateInCompanyAsync(Guid candidateId, Guid recruiterId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<MatchView>> ListMatchViewsByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<MatchView>> ListMatchViewsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
