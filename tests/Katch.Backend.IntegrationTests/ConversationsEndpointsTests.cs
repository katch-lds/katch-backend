// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using static Katch.Backend.IntegrationTests.ConversationTestData;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes das conversas (documentação da API v02, secções 6.8.1 a 6.8.4), sobre SQLite in-memory. Cada teste grava as
/// suas próprias contas, por isso a fábrica pode ser partilhada. A marca de concorrência <c>xmin</c> é exercitada nos
/// testes unitários, porque o SQLite não a tem.
/// </summary>
public sealed class ConversationsEndpointsTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string ReadOnlyMessage = "A conversa está em modo apenas de consulta.";
    private const string NoMatchMessage = "Não existe match para esta conversa.";
    private const string ForbiddenMessage = "Não tem autorização para aceder a este elemento.";
    private const string NotFoundMessage = "O elemento pedido não existe ou não está disponível.";
    private const string Err06Message = "A operação não está disponível para este tipo de conta.";

    private static readonly DateTimeOffset Base = new(2026, 10, 9, 16, 0, 0, TimeSpan.Zero);

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationsEndpointsTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public ConversationsEndpointsTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// A lista traz só as conversas do utilizador, com a outra parte, o estado, a última mensagem e as mensagens por ler
    /// da outra parte (6.8.1).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListShowsOnlyTheConversationsOfTheUserWithTheUnreadCount()
    {
        Scenario s = await this.SeedScenarioAsync();
        await SeedMessageAsync(this.factory, s.MatchId, s.Candidate.User.Id, "Olá", Base.AddMinutes(1), Base.AddMinutes(2));
        await SeedMessageAsync(this.factory, s.MatchId, s.Company.User.Id, "Boa tarde", Base.AddMinutes(3));
        await SeedMessageAsync(this.factory, s.MatchId, s.Company.User.Id, "Quando podemos falar?", Base.AddMinutes(4));
        await SeedMessageAsync(this.factory, s.MatchId, s.Candidate.User.Id, "Amanhã", Base.AddMinutes(5));

        JsonElement candidateItem = Assert.Single(await this.ListAsync(s.Candidate.Token));
        JsonElement recruiterItem = Assert.Single(await this.ListAsync(s.Company.Token));

        Assert.Equal(s.MatchId, candidateItem.GetProperty("matchId").GetGuid());
        Assert.Equal("Rui Matos", candidateItem.GetProperty("counterpartName").GetString());
        Assert.Equal("Programador Backend .NET", candidateItem.GetProperty("jobTitle").GetString());
        Assert.Equal("Lumen Software, Lda.", candidateItem.GetProperty("companyName").GetString());
        Assert.Equal("OPEN", candidateItem.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, candidateItem.GetProperty("closeReason").ValueKind);
        Assert.Equal(JsonValueKind.Null, candidateItem.GetProperty("closedBy").ValueKind);
        Assert.Equal(Base.AddMinutes(5), candidateItem.GetProperty("lastMessageAt").GetDateTimeOffset());
        Assert.Equal(2, candidateItem.GetProperty("unreadCount").GetInt32());

        Assert.Equal("Ana Ferreira", recruiterItem.GetProperty("counterpartName").GetString());
        Assert.Equal(1, recruiterItem.GetProperty("unreadCount").GetInt32());
    }

    /// <summary>
    /// Os terceiros nunca vêem a conversa: nem outro Candidato, nem o Recrutador de outra Empresa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListNeverShowsTheConversationsOfOthers()
    {
        Scenario s = await this.SeedScenarioAsync();
        CandidateSeed otherCandidate = await SeedCandidateAsync(this.factory);
        CompanySeed otherCompany = await SeedCompanyAsync(this.factory);

        Assert.Empty(await this.ListAsync(otherCandidate.Token));
        Assert.Empty(await this.ListAsync(otherCompany.Token));
        Assert.Single(await this.ListAsync(s.Candidate.Token));
    }

    /// <summary>
    /// A lista inclui as conversas encerradas com o motivo e o autor: só o encerramento por uma das partes tem autor.
    /// </summary>
    /// <param name="kind">Estado do match.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <param name="hasAuthor">Se o encerramento tem autor.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(MatchKind.ClosedByParty, "CLOSED_BY_PARTY", true)]
    [InlineData(MatchKind.ClosedAccount, "ACCOUNT_BLOCKED_OR_SUSPENDED", false)]
    [InlineData(MatchKind.ClosedCompany, "COMPANY_SUSPENDED", false)]
    public async Task ListShowsTheClosedConversationsWithTheReasonAndAuthor(MatchKind kind, string reason, bool hasAuthor)
    {
        Scenario s = await this.SeedScenarioAsync(kind);

        JsonElement item = Assert.Single(await this.ListAsync(s.Company.Token));

        Assert.Equal("CLOSED", item.GetProperty("status").GetString());
        Assert.Equal(reason, item.GetProperty("closeReason").GetString());
        if (hasAuthor)
        {
            Assert.Equal(s.Candidate.User.Id, item.GetProperty("closedBy").GetGuid());
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, item.GetProperty("closedBy").ValueKind);
        }

        Assert.Equal(JsonValueKind.Null, item.GetProperty("lastMessageAt").ValueKind);
    }

    /// <summary>
    /// A ordem põe primeiro as conversas sem mensagens, pela data do match, e depois as outras pela última mensagem.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListOrdersTheConversationsWithoutMessagesFirstAndThenByTheLastMessage()
    {
        CompanySeed company = await SeedCompanyAsync(this.factory);
        CandidateSeed candidate = await SeedCandidateAsync(this.factory);
        Guid emptyOld = await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "A"), matchedAt: Base.AddDays(-9));
        Guid emptyNew = await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "B"), matchedAt: Base.AddDays(-2));
        Guid talkedRecent = await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "C"), matchedAt: Base.AddDays(-8));
        Guid talkedOld = await SeedMatchAsync(this.factory, candidate, company, await SeedJobAsync(this.factory, company, "D"), matchedAt: Base.AddDays(-1));
        await SeedMessageAsync(this.factory, talkedRecent, company.User.Id, "Olá", Base.AddHours(-1));
        await SeedMessageAsync(this.factory, talkedOld, company.User.Id, "Olá", Base.AddHours(-5));

        List<JsonElement> items = await this.ListAsync(candidate.Token);

        Assert.Equal(
            [emptyNew, emptyOld, talkedRecent, talkedOld],
            items.Select(item => item.GetProperty("matchId").GetGuid()).ToList());
    }

    /// <summary>
    /// O Administrador recebe 403 ERR-06 em todas as operações das conversas (RF103).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AdminGetsErr06OnEveryOperation()
    {
        Scenario s = await this.SeedScenarioAsync();
        string token = await SeedAdminTokenAsync(this.factory);

        foreach ((HttpMethod method, string path) in new[]
        {
            (HttpMethod.Get, "/api/conversations"),
            (HttpMethod.Get, $"/api/conversations/{s.MatchId}/messages"),
            (HttpMethod.Post, $"/api/conversations/{s.MatchId}/messages"),
            (HttpMethod.Post, $"/api/conversations/{s.MatchId}/close"),
        })
        {
            using HttpResponseMessage response = await SendAsync(this.factory, method, path, token, method == HttpMethod.Post ? new { content = "Olá" } : null);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
            Assert.Equal("ERR-06", error.GetProperty("code").GetString());
            Assert.Equal(Err06Message, error.GetProperty("message").GetString());
        }
    }

    /// <summary>
    /// Sem credencial todas as operações respondem 401 ERR-01.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WithoutCredentialEveryOperationGetsErr01()
    {
        Guid matchId = Guid.NewGuid();

        foreach ((HttpMethod method, string path) in new[]
        {
            (HttpMethod.Get, "/api/conversations"),
            (HttpMethod.Get, $"/api/conversations/{matchId}/messages"),
            (HttpMethod.Post, $"/api/conversations/{matchId}/messages"),
            (HttpMethod.Post, $"/api/conversations/{matchId}/close"),
        })
        {
            using HttpResponseMessage response = await SendAsync(this.factory, method, path, token: null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("ERR-01", (await AuthTestAccounts.ReadJsonAsync(response)).GetProperty("code").GetString());
        }
    }

    /// <summary>
    /// Sem <c>after</c> abre o histórico completo, da mais antiga para a mais recente, com os dados de cada mensagem
    /// (6.8.2).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenReturnsTheWholeHistoryFromTheOldestToTheNewest()
    {
        Scenario s = await this.SeedScenarioAsync();
        Guid second = await SeedMessageAsync(this.factory, s.MatchId, s.Company.User.Id, "Segunda", Base.AddMinutes(2));
        Guid first = await SeedMessageAsync(this.factory, s.MatchId, s.Candidate.User.Id, "Primeira", Base.AddMinutes(1));

        List<JsonElement> messages = await this.OpenAsync(s.Candidate.Token, s.MatchId);

        Assert.Equal([first, second], messages.Select(message => message.GetProperty("messageId").GetGuid()).ToList());
        Assert.Equal("Primeira", messages[0].GetProperty("content").GetString());
        Assert.Equal(s.Candidate.User.Id, messages[0].GetProperty("senderId").GetGuid());
        Assert.Equal(Base.AddMinutes(1), messages[0].GetProperty("sentAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, messages[0].GetProperty("readAt").ValueKind);
    }

    /// <summary>
    /// Com <c>after</c> só vêm as mensagens estritamente posteriores; sem novidades a lista é vazia.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenWithAfterReturnsOnlyTheStrictlyLaterMessages()
    {
        Scenario s = await this.SeedScenarioAsync();
        await SeedMessageAsync(this.factory, s.MatchId, s.Company.User.Id, "Antiga", Base.AddMinutes(1));
        Guid boundary = await SeedMessageAsync(this.factory, s.MatchId, s.Company.User.Id, "No limite", Base.AddMinutes(2));
        Guid later = await SeedMessageAsync(this.factory, s.MatchId, s.Company.User.Id, "Nova", Base.AddMinutes(3));

        List<JsonElement> messages = await this.OpenAsync(s.Candidate.Token, s.MatchId, Base.AddMinutes(2));
        List<JsonElement> none = await this.OpenAsync(s.Candidate.Token, s.MatchId, Base.AddMinutes(3));

        Assert.NotEqual(boundary, later);
        Assert.Equal([later], messages.Select(message => message.GetProperty("messageId").GetGuid()).ToList());
        Assert.Empty(none);
    }

    /// <summary>
    /// Um <c>after</c> mal formado dá 400 ERR-10 no campo <c>after</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenWithAMalformedAfterGetsErr10()
    {
        Scenario s = await this.SeedScenarioAsync();

        using HttpResponseMessage response = await SendAsync(
            this.factory,
            HttpMethod.Get,
            $"/api/conversations/{s.MatchId}/messages?after=ontem",
            s.Candidate.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-10", error.GetProperty("code").GetString());
        Assert.Equal("after", Assert.Single(AuthTestAccounts.Fields(error)).Field);
    }

    /// <summary>
    /// Abrir marca como lidas só as mensagens da outra parte, só uma vez, e a resposta já traz o instante de leitura.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenMarksOnlyTheOtherPartysMessagesAsReadAndOnlyOnce()
    {
        Scenario s = await this.SeedScenarioAsync();
        Guid fromRecruiter = await SeedMessageAsync(this.factory, s.MatchId, s.Company.User.Id, "Olá", Base.AddMinutes(1));
        Guid own = await SeedMessageAsync(this.factory, s.MatchId, s.Candidate.User.Id, "Olá, boa tarde", Base.AddMinutes(2));
        DateTimeOffset firstRead = DateTimeOffset.UtcNow;
        this.factory.Clock.Fixed = firstRead;

        List<JsonElement> opened = await this.OpenAsync(s.Candidate.Token, s.MatchId);

        Assert.Equal(firstRead, opened.Single(message => message.GetProperty("messageId").GetGuid() == fromRecruiter).GetProperty("readAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, opened.Single(message => message.GetProperty("messageId").GetGuid() == own).GetProperty("readAt").ValueKind);

        this.factory.Clock.Fixed = firstRead.AddMinutes(10);
        List<JsonElement> again = await this.OpenAsync(s.Candidate.Token, s.MatchId);

        Assert.Equal(firstRead, again.Single(message => message.GetProperty("messageId").GetGuid() == fromRecruiter).GetProperty("readAt").GetDateTimeOffset());
        List<Message> stored = await ReadMessagesAsync(this.factory, s.MatchId);
        Assert.Equal(firstRead, stored.Single(message => message.Id == fromRecruiter).ReadAt);
        Assert.Null(stored.Single(message => message.Id == own).ReadAt);
        Assert.Equal(0, Assert.Single(await this.ListAsync(s.Candidate.Token)).GetProperty("unreadCount").GetInt32());
        Assert.Equal(1, Assert.Single(await this.ListAsync(s.Company.Token)).GetProperty("unreadCount").GetInt32());
    }

    /// <summary>
    /// A conversa em modo apenas de consulta continua a poder ser aberta.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OpenWorksOnAConversationInReadOnlyMode()
    {
        Scenario s = await this.SeedScenarioAsync(MatchKind.ClosedCompany);
        Guid messageId = await SeedMessageAsync(this.factory, s.MatchId, s.Company.User.Id, "Olá", Base);

        List<JsonElement> messages = await this.OpenAsync(s.Candidate.Token, s.MatchId);

        Assert.Equal(messageId, Assert.Single(messages).GetProperty("messageId").GetGuid());
    }

    /// <summary>
    /// Os terceiros recebem 403 ERR-08 em todas as operações sobre a conversa, sem que nada seja alterado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ThirdPartiesGetErr08OnEveryOperation()
    {
        Scenario s = await this.SeedScenarioAsync();
        CandidateSeed otherCandidate = await SeedCandidateAsync(this.factory);
        CompanySeed otherCompany = await SeedCompanyAsync(this.factory);

        foreach (string token in new[] { otherCandidate.Token, otherCompany.Token })
        {
            foreach ((HttpMethod method, string path) in new[]
            {
                (HttpMethod.Get, $"/api/conversations/{s.MatchId}/messages"),
                (HttpMethod.Post, $"/api/conversations/{s.MatchId}/messages"),
                (HttpMethod.Post, $"/api/conversations/{s.MatchId}/close"),
            })
            {
                using HttpResponseMessage response = await SendAsync(this.factory, method, path, token, method == HttpMethod.Post ? new { content = "Olá" } : null);

                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
                Assert.Equal("ERR-08", error.GetProperty("code").GetString());
                Assert.Equal(ForbiddenMessage, error.GetProperty("message").GetString());
            }
        }

        Assert.Empty(await ReadMessagesAsync(this.factory, s.MatchId));
        Assert.Equal(ConversationStatus.Open, (await ReadMatchAsync(this.factory, s.MatchId)).ConversationStatus);
    }

    /// <summary>
    /// Um match inexistente dá 404 ERR-20 em todas as operações.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AnUnknownMatchGetsErr20OnEveryOperation()
    {
        CandidateSeed candidate = await SeedCandidateAsync(this.factory);
        Guid matchId = Guid.NewGuid();

        foreach ((HttpMethod method, string path) in new[]
        {
            (HttpMethod.Get, $"/api/conversations/{matchId}/messages"),
            (HttpMethod.Post, $"/api/conversations/{matchId}/messages"),
            (HttpMethod.Post, $"/api/conversations/{matchId}/close"),
        })
        {
            using HttpResponseMessage response = await SendAsync(this.factory, method, path, candidate.Token, method == HttpMethod.Post ? new { content = "Olá" } : null);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
            Assert.Equal("ERR-20", error.GetProperty("code").GetString());
            Assert.Equal(NotFoundMessage, error.GetProperty("message").GetString());
        }
    }

    /// <summary>
    /// Um interesse em espera ou recusado não tem conversa: 409 ERR-37 em todas as operações.
    /// </summary>
    /// <param name="kind">Estado do match.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(MatchKind.Waiting)]
    [InlineData(MatchKind.Declined)]
    [InlineData(MatchKind.Rejected)]
    public async Task AMatchThatIsNotConfirmedGetsErr37OnEveryOperation(MatchKind kind)
    {
        Scenario s = await this.SeedScenarioAsync(kind);

        foreach ((HttpMethod method, string path) in new[]
        {
            (HttpMethod.Get, $"/api/conversations/{s.MatchId}/messages"),
            (HttpMethod.Post, $"/api/conversations/{s.MatchId}/messages"),
            (HttpMethod.Post, $"/api/conversations/{s.MatchId}/close"),
        })
        {
            using HttpResponseMessage response = await SendAsync(this.factory, method, path, s.Candidate.Token, method == HttpMethod.Post ? new { content = "Olá" } : null);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
            Assert.Equal("ERR-37", error.GetProperty("code").GetString());
            Assert.Equal(NoMatchMessage, error.GetProperty("message").GetString());
        }

        Assert.Empty(await ReadMessagesAsync(this.factory, s.MatchId));
        Assert.Empty(await this.ListAsync(s.Candidate.Token));
    }

    /// <summary>
    /// O envio válido responde 201 com a mensagem gravada, sem <c>Location</c>, e a outra parte vê-a na consulta seguinte
    /// (6.8.3).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SendReturns201WithTheSavedMessageAndTheOtherPartySeesIt()
    {
        Scenario s = await this.SeedScenarioAsync();
        DateTimeOffset sentAt = DateTimeOffset.UtcNow.AddSeconds(-30);
        this.factory.Clock.Fixed = sentAt;

        using HttpResponseMessage response = await SendAsync(
            this.factory,
            HttpMethod.Post,
            $"/api/conversations/{s.MatchId}/messages",
            s.Candidate.Token,
            new { content = "  Boa tarde!  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        JsonElement message = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("  Boa tarde!  ", message.GetProperty("content").GetString());
        Assert.Equal(s.Candidate.User.Id, message.GetProperty("senderId").GetGuid());
        Assert.Equal(sentAt, message.GetProperty("sentAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, message.GetProperty("readAt").ValueKind);

        this.factory.Clock.Fixed = null;
        List<JsonElement> seen = await this.OpenAsync(s.Company.Token, s.MatchId, sentAt.AddSeconds(-1));
        Assert.Equal(message.GetProperty("messageId").GetGuid(), Assert.Single(seen).GetProperty("messageId").GetGuid());
    }

    /// <summary>
    /// Um texto de 1 e de 1000 caracteres é aceite.
    /// </summary>
    /// <param name="length">Número de caracteres.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task SendAcceptsTheLimitLengths(int length)
    {
        Scenario s = await this.SeedScenarioAsync();

        using HttpResponseMessage response = await SendAsync(
            this.factory,
            HttpMethod.Post,
            $"/api/conversations/{s.MatchId}/messages",
            s.Candidate.Token,
            new { content = new string('a', length) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Um texto vazio, só de espaços, em falta ou com 1001 caracteres dá 400 ERR-10 no campo <c>content</c>.
    /// </summary>
    /// <param name="content">Texto enviado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData(null)]
    [InlineData("TOO_LONG")]
    public async Task SendRejectsAnInvalidContent(string? content)
    {
        Scenario s = await this.SeedScenarioAsync();
        string? text = content == "TOO_LONG" ? new string('a', 1001) : content;

        using HttpResponseMessage response = await SendAsync(
            this.factory,
            HttpMethod.Post,
            $"/api/conversations/{s.MatchId}/messages",
            s.Candidate.Token,
            new { content = text });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal("ERR-10", error.GetProperty("code").GetString());
        Assert.Equal("content", Assert.Single(AuthTestAccounts.Fields(error)).Field);
        Assert.Empty(await ReadMessagesAsync(this.factory, s.MatchId));
    }

    /// <summary>
    /// O estado vem antes do conteúdo: numa conversa encerrada, mesmo com texto inválido, a resposta é 409 ERR-36.
    /// </summary>
    /// <param name="kind">Estado do match.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(MatchKind.ClosedByParty)]
    [InlineData(MatchKind.ClosedAccount)]
    [InlineData(MatchKind.ClosedCompany)]
    public async Task SendOnAClosedConversationGetsErr36(MatchKind kind)
    {
        Scenario s = await this.SeedScenarioAsync(kind);

        foreach (string content in new[] { "Olá", string.Empty })
        {
            using HttpResponseMessage response = await SendAsync(
                this.factory,
                HttpMethod.Post,
                $"/api/conversations/{s.MatchId}/messages",
                s.Company.Token,
                new { content });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
            Assert.Equal("ERR-36", error.GetProperty("code").GetString());
            Assert.Equal(ReadOnlyMessage, error.GetProperty("message").GetString());
        }

        Assert.Empty(await ReadMessagesAsync(this.factory, s.MatchId));
    }

    /// <summary>
    /// Encerrar responde 204, regista o motivo, o autor e a data, as duas partes veem o novo estado, o envio passa a dar
    /// ERR-36 e o histórico mantém-se (6.8.4).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CloseReturns204AndPutsTheConversationInReadOnlyModeForBothParties()
    {
        Scenario s = await this.SeedScenarioAsync();
        Guid messageId = await SeedMessageAsync(this.factory, s.MatchId, s.Candidate.User.Id, "Olá", Base);
        DateTimeOffset closedAt = DateTimeOffset.UtcNow;
        this.factory.Clock.Fixed = closedAt;

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Post, $"/api/conversations/{s.MatchId}/close", s.Company.Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Match match = await ReadMatchAsync(this.factory, s.MatchId);
        Assert.Equal(ConversationStatus.Closed, match.ConversationStatus);
        Assert.Equal(ConversationCloseReason.ClosedByParty, match.ConversationCloseReason);
        Assert.Equal(s.Company.User.Id, match.ConversationClosedById);
        Assert.Equal(closedAt, match.ConversationClosedAt);

        foreach (string token in new[] { s.Company.Token, s.Candidate.Token })
        {
            JsonElement item = Assert.Single(await this.ListAsync(token));
            Assert.Equal("CLOSED", item.GetProperty("status").GetString());
            Assert.Equal("CLOSED_BY_PARTY", item.GetProperty("closeReason").GetString());
            Assert.Equal(s.Company.User.Id, item.GetProperty("closedBy").GetGuid());
        }

        using HttpResponseMessage send = await SendAsync(
            this.factory,
            HttpMethod.Post,
            $"/api/conversations/{s.MatchId}/messages",
            s.Candidate.Token,
            new { content = "Ainda aí?" });
        Assert.Equal(HttpStatusCode.Conflict, send.StatusCode);
        Assert.Equal("ERR-36", (await AuthTestAccounts.ReadJsonAsync(send)).GetProperty("code").GetString());

        this.factory.Clock.Fixed = null;
        Assert.Equal(messageId, Assert.Single(await this.OpenAsync(s.Candidate.Token, s.MatchId)).GetProperty("messageId").GetGuid());
    }

    /// <summary>
    /// Encerrar uma conversa já encerrada, por qualquer motivo, dá 409 ERR-36 e não altera o encerramento anterior.
    /// </summary>
    /// <param name="kind">Estado do match.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(MatchKind.ClosedByParty)]
    [InlineData(MatchKind.ClosedAccount)]
    [InlineData(MatchKind.ClosedCompany)]
    public async Task ClosingAConversationThatIsAlreadyClosedGetsErr36(MatchKind kind)
    {
        Scenario s = await this.SeedScenarioAsync(kind);
        Match before = await ReadMatchAsync(this.factory, s.MatchId);

        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Post, $"/api/conversations/{s.MatchId}/close", s.Candidate.Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ERR-36", (await AuthTestAccounts.ReadJsonAsync(response)).GetProperty("code").GetString());
        Match after = await ReadMatchAsync(this.factory, s.MatchId);
        Assert.Equal(before.ConversationCloseReason, after.ConversationCloseReason);
        Assert.Equal(before.ConversationClosedAt, after.ConversationClosedAt);
        Assert.Equal(before.ConversationClosedById, after.ConversationClosedById);
    }

    /// <summary>
    /// Consulta periódica (P17): quem consulta com <c>after</c> igual à última mensagem que já tinha recebe só a nova.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PollingWithAfterReturnsOnlyTheNewMessageOnTheFirstRequestAfterTheSend()
    {
        Scenario s = await this.SeedScenarioAsync();
        DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-10);
        this.factory.Clock.Fixed = start;
        JsonElement known = await this.SendAndReadAsync(s.Candidate.Token, s.MatchId, "Primeira");
        DateTimeOffset lastKnown = known.GetProperty("sentAt").GetDateTimeOffset();

        List<JsonElement> nothing = await this.OpenAsync(s.Company.Token, s.MatchId, lastKnown);
        Assert.Empty(nothing);

        this.factory.Clock.Fixed = start.AddSeconds(3);
        JsonElement reply = await this.SendAndReadAsync(s.Company.Token, s.MatchId, "Resposta");

        this.factory.Clock.Fixed = start.AddSeconds(4);
        List<JsonElement> news = await this.OpenAsync(s.Candidate.Token, s.MatchId, lastKnown);

        Assert.Equal(reply.GetProperty("messageId").GetGuid(), Assert.Single(news).GetProperty("messageId").GetGuid());
        Assert.Equal("Resposta", news[0].GetProperty("content").GetString());
    }

    private async Task<JsonElement> SendAndReadAsync(string token, Guid matchId, string content)
    {
        using HttpResponseMessage response = await SendAsync(
            this.factory,
            HttpMethod.Post,
            $"/api/conversations/{matchId}/messages",
            token,
            new { content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AuthTestAccounts.ReadJsonAsync(response);
    }

    private async Task<List<JsonElement>> ListAsync(string token)
    {
        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Get, "/api/conversations", token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().ToList();
    }

    private async Task<List<JsonElement>> OpenAsync(string token, Guid matchId, DateTimeOffset? after = null)
    {
        string query = after is null ? string.Empty : "?after=" + Uri.EscapeDataString(after.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        using HttpResponseMessage response = await SendAsync(this.factory, HttpMethod.Get, $"/api/conversations/{matchId}/messages{query}", token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await AuthTestAccounts.ReadJsonAsync(response)).EnumerateArray().ToList();
    }

    private async Task<Scenario> SeedScenarioAsync(MatchKind kind = MatchKind.Open)
    {
        CompanySeed company = await SeedCompanyAsync(this.factory);
        CandidateSeed candidate = await SeedCandidateAsync(this.factory);
        Guid jobId = await SeedJobAsync(this.factory, company);
        Guid matchId = await SeedMatchAsync(this.factory, candidate, company, jobId, kind, Base.AddDays(-1));
        return new Scenario(company, candidate, matchId);
    }

    private sealed record Scenario(CompanySeed Company, CandidateSeed Candidate, Guid MatchId);
}
