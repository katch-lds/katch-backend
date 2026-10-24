// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de persistência de <see cref="Message"/> sobre SQLite in-memory: gravação e leitura, marca de leitura e
/// chaves estrangeiras para o match e para o remetente. Os <c>CHECK</c> e o filtro do índice são específicos do
/// PostgreSQL e estão verificados nos testes unitários do mapeamento e na evidência da migration.
/// </summary>
public sealed class MessagePersistenceTests : IClassFixture<KatchWebApplicationFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

    private static int taxIdCounter;

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagePersistenceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public MessagePersistenceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// A mensagem é gravada com o match, o remetente, o texto exato e a data de envio, ainda por ler, e lida de volta
    /// igual.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MessageIsSavedAndReadBack()
    {
        (Match match, AppUser candidateUser, _) = await this.SeedAsync("gravar");
        var message = Message.Send(match.Id, candidateUser.Id, "  Olá!\r\nQuando podemos falar?  ", Now);
        await this.SaveAsync(message);

        Message saved = await this.ReadAsync(message.Id);

        Assert.Equal(match.Id, saved.MatchId);
        Assert.Equal(candidateUser.Id, saved.SenderId);
        Assert.Equal("  Olá!\r\nQuando podemos falar?  ", saved.Content);
        Assert.Equal(Now, saved.SentAt);
        Assert.Null(saved.ReadAt);
    }

    /// <summary>
    /// O texto com o comprimento máximo de 1000 caracteres é gravado e lido por inteiro.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MessageWith1000CharactersIsSavedAndReadBack()
    {
        (Match match, AppUser candidateUser, _) = await this.SeedAsync("limite");
        var message = Message.Send(match.Id, candidateUser.Id, new string('x', 1000), Now);
        await this.SaveAsync(message);

        Message saved = await this.ReadAsync(message.Id);

        Assert.Equal(1000, saved.Content.Length);
    }

    /// <summary>
    /// A mensagem lida é gravada com a data da leitura.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReadMessageIsSavedWithTheReadingDate()
    {
        (Match match, _, AppUser recruiterUser) = await this.SeedAsync("lida");
        var message = Message.Send(match.Id, recruiterUser.Id, "Bom dia", Now);
        await this.SaveAsync(message);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Message stored = await context.Messages.SingleAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);
            stored.MarkRead(Now.AddMinutes(4));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Message saved = await this.ReadAsync(message.Id);

        Assert.Equal(Now.AddMinutes(4), saved.ReadAt);
        Assert.Equal(Now, saved.SentAt);
    }

    /// <summary>
    /// As mensagens de uma conversa leem-se por ordem de envio, sem as de outras conversas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MessagesOfAConversationAreReadInSendingOrder()
    {
        (Match match, AppUser candidateUser, AppUser recruiterUser) = await this.SeedAsync("ordem");
        (Match other, AppUser otherCandidateUser, _) = await this.SeedAsync("outra");
        await this.SaveAsync(Message.Send(match.Id, recruiterUser.Id, "segunda", Now.AddMinutes(2)));
        await this.SaveAsync(Message.Send(match.Id, candidateUser.Id, "primeira", Now.AddMinutes(1)));
        await this.SaveAsync(Message.Send(other.Id, otherCandidateUser.Id, "de outra conversa", Now));

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        List<Message> conversation = await context.Messages
            .AsNoTracking()
            .Where(item => item.MatchId == match.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        // O SQLite não ordena DateTimeOffset; a ordenação é feita em memória.
        Assert.Equal(["primeira", "segunda"], conversation.OrderBy(item => item.SentAt).Select(item => item.Content).ToArray());
    }

    /// <summary>
    /// Um match inexistente é rejeitado pela chave estrangeira.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownMatchIsRejectedByTheForeignKey()
    {
        (_, AppUser candidateUser, _) = await this.SeedAsync("match-fk");

        await Assert.ThrowsAsync<DbUpdateException>(
            () => this.SaveAsync(Message.Send(Guid.NewGuid(), candidateUser.Id, "Olá", Now)));
    }

    /// <summary>
    /// Um remetente inexistente é rejeitado pela chave estrangeira.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownSenderIsRejectedByTheForeignKey()
    {
        (Match match, _, _) = await this.SeedAsync("remetente-fk");

        await Assert.ThrowsAsync<DbUpdateException>(
            () => this.SaveAsync(Message.Send(match.Id, Guid.NewGuid(), "Olá", Now)));
    }

    private async Task<(Match Match, AppUser CandidateUser, AppUser RecruiterUser)> SeedAsync(string name)
    {
        var recruiterUser = new AppUser($"recrutador-{name}@katch.pt", "hash", UserType.Recruiter, Now, Now);
        var recruiter = new Recruiter(recruiterUser);
        var location = new Location($"Braga {name}", "Braga", 41.545450m, -8.426507m);
        var company = new Company(
            recruiter.Id,
            new CompanyRegistration
            {
                CompanyName = "Lumen Software, Lda.",
                TaxId = (509_600_000 + Interlocked.Increment(ref taxIdCounter)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Industry = Industry.Technology,
                Address = "Avenida da Liberdade, 100, 4710-000 Braga",
                LocationId = location.Id,
                ContactEmail = "geral@lumen-exemplo.pt",
                ContactPhone = "253000111",
                ResponsibleName = "Rui Matos",
            },
            Now);
        company.Approve();
        var job = new Job(
            company.Id,
            new JobData
            {
                Title = "Programador Backend .NET",
                Description = "Desenvolvimento de APIs REST em ASP.NET Core.",
                MinSalary = 1600m,
                MaxSalary = 2100m,
                LocationId = location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        var candidateUser = new AppUser($"candidato-{name}@katch.pt", "hash", UserType.Candidate, Now, Now);
        var candidate = new Candidate(candidateUser, "Ana Ferreira", "912345678", location.Id);
        var match = Match.ExpressInterest(candidate.Id, job.Id, Now);
        match.OpenProfile(Now);
        match.Accept(recruiter.Id, Now.AddMinutes(1));

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        context.Recruiters.Add(recruiter);
        context.Locations.Add(location);
        context.Companies.Add(company);
        context.Jobs.Add(job);
        context.Candidates.Add(candidate);
        context.Matches.Add(match);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (match, candidateUser, recruiterUser);
    }

    private async Task SaveAsync(Message message)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        context.Messages.Add(message);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Message> ReadAsync(Guid messageId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Messages
            .AsNoTracking()
            .SingleAsync(item => item.Id == messageId, TestContext.Current.CancellationToken);
    }
}
