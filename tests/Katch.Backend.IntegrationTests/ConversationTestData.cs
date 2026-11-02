// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Dados e pedidos partilhados pelos testes dos matches e das conversas: Empresas, vagas, Candidatos e matches
/// <c>MATCHED</c>, <c>WAITING</c> e recusados, com a conversa aberta ou encerrada por cada motivo. Os dados são fictícios.
/// </summary>
public static class ConversationTestData
{
    private static long taxIdCounter = 800_000_000;

    /// <summary>
    /// Estado em que um match é gravado.
    /// </summary>
    public enum MatchKind
    {
        /// <summary>Vaga recusada pelo Candidato.</summary>
        Declined,

        /// <summary>Interesse em espera.</summary>
        Waiting,

        /// <summary>Interesse recusado pelo Recrutador.</summary>
        Rejected,

        /// <summary>Match com a conversa aberta.</summary>
        Open,

        /// <summary>Conversa encerrada por uma das partes.</summary>
        ClosedByParty,

        /// <summary>Conversa passada a consulta por bloqueio ou suspensão da conta.</summary>
        ClosedAccount,

        /// <summary>Conversa passada a consulta pela suspensão da Empresa.</summary>
        ClosedCompany,
    }

    /// <summary>
    /// Grava um Recrutador com a Empresa no estado indicado.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="status">Estado da Empresa.</param>
    /// <param name="logoPath">Caminho do logótipo; <see langword="null"/> sem logótipo.</param>
    /// <param name="responsibleName">Nome do responsável da Empresa.</param>
    /// <param name="companyName">Designação da Empresa.</param>
    /// <returns>O Recrutador e a Empresa.</returns>
    internal static async Task<CompanySeed> SeedCompanyAsync(
        KatchWebApplicationFactory factory,
        CompanyStatus status = CompanyStatus.Approved,
        string? logoPath = null,
        string responsibleName = "Rui Matos",
        string companyName = "Lumen Software, Lda.")
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Recruiter, created, created);
        var recruiter = new Recruiter(user);
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        var company = new Company(
            recruiter.Id,
            new CompanyRegistration
            {
                CompanyName = companyName,
                TaxId = Interlocked.Increment(ref taxIdCounter).ToString(CultureInfo.InvariantCulture),
                Industry = Industry.Technology,
                Address = "Rua de Exemplo, 1, 4700-000 Braga",
                LocationId = location.Id,
                ContactEmail = "recrutamento@lumen-exemplo.pt",
                ContactPhone = "253000222",
                ResponsibleName = responsibleName,
            },
            created);
        switch (status)
        {
            case CompanyStatus.Approved:
                company.Approve();
                break;
            case CompanyStatus.Rejected:
                company.Reject("Documentação em falta no pedido.");
                break;
            case CompanyStatus.Suspended:
                company.Approve();
                company.Suspend();
                break;
            default:
                break;
        }

        if (logoPath is not null)
        {
            company.SetLogo(logoPath);
        }

        await SaveAsync(factory, context =>
        {
            context.Recruiters.Add(recruiter);
            context.Locations.Add(location);
            context.Companies.Add(company);
        });
        return new CompanySeed(user, company, location, IssueToken(factory, user));
    }

    /// <summary>
    /// Grava um Candidato novo e emite a credencial de sessão.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="fullName">Nome do Candidato.</param>
    /// <param name="phone">Contacto telefónico.</param>
    /// <returns>O Candidato.</returns>
    internal static async Task<CandidateSeed> SeedCandidateAsync(
        KatchWebApplicationFactory factory,
        string fullName = "Ana Ferreira",
        string phone = "912345678")
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Candidate, created, created);
        var location = new Location($"Porto {Guid.NewGuid():N}", "Porto", 41.1579m, -8.6291m);
        var candidate = new Candidate(user, fullName, phone, location.Id);
        await SaveAsync(factory, context =>
        {
            context.Locations.Add(location);
            context.Candidates.Add(candidate);
        });
        return new CandidateSeed(user, candidate, IssueToken(factory, user));
    }

    /// <summary>
    /// Grava a conta de um Administrador e emite a credencial de sessão.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <returns>A credencial de sessão.</returns>
    internal static async Task<string> SeedAdminTokenAsync(KatchWebApplicationFactory factory)
    {
        AppUser admin = await AuthTestAccounts.SeedAsync(factory, UserType.Admin);
        return IssueToken(factory, admin);
    }

    /// <summary>
    /// Grava uma vaga da Empresa, publicada e depois no estado indicado.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="company">Empresa dona da vaga.</param>
    /// <param name="title">Função da vaga.</param>
    /// <param name="status">Estado final da vaga.</param>
    /// <returns>O identificador da vaga.</returns>
    internal static async Task<Guid> SeedJobAsync(
        KatchWebApplicationFactory factory,
        CompanySeed company,
        string title = "Programador Backend .NET",
        JobStatus status = JobStatus.Published)
    {
        DateTimeOffset published = DateTimeOffset.UtcNow.AddDays(-10);
        var job = new Job(
            company.Company.Id,
            new JobData
            {
                Title = title,
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = company.Location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        switch (status)
        {
            case JobStatus.Published:
                job.Publish(published);
                break;
            case JobStatus.Suspended:
                job.Publish(published);
                job.Suspend(published.AddDays(1));
                break;
            case JobStatus.Closed:
                job.Close();
                break;
            default:
                break;
        }

        await SaveAsync(factory, context => context.Jobs.Add(job));
        return job.Id;
    }

    /// <summary>
    /// Grava a resposta do Candidato à vaga no estado indicado.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="candidate">Candidato.</param>
    /// <param name="company">Empresa dona da vaga.</param>
    /// <param name="jobId">Vaga.</param>
    /// <param name="kind">Estado do match.</param>
    /// <param name="matchedAt">Instante da aceitação pelo Recrutador.</param>
    /// <returns>O identificador do match, que é o da conversa.</returns>
    internal static async Task<Guid> SeedMatchAsync(
        KatchWebApplicationFactory factory,
        CandidateSeed candidate,
        CompanySeed company,
        Guid jobId,
        MatchKind kind = MatchKind.Open,
        DateTimeOffset? matchedAt = null)
    {
        DateTimeOffset at = matchedAt ?? DateTimeOffset.UtcNow.AddDays(-1);
        Guid recruiterId = company.User.Id;

        Match match;
        if (kind == MatchKind.Declined)
        {
            match = Match.Decline(candidate.Candidate.Id, jobId, at.AddHours(-2));
        }
        else
        {
            match = Match.ExpressInterest(candidate.Candidate.Id, jobId, at.AddHours(-2));
            if (kind != MatchKind.Waiting)
            {
                match.OpenProfile(at.AddHours(-1));
                if (kind == MatchKind.Rejected)
                {
                    match.Reject(recruiterId, at);
                }
                else
                {
                    match.Accept(recruiterId, at);
                }
            }
        }

        switch (kind)
        {
            case MatchKind.ClosedByParty:
                match.CloseConversation(candidate.User.Id, ConversationCloseReason.ClosedByParty, at.AddHours(1));
                break;
            case MatchKind.ClosedAccount:
                match.CloseConversation(null, ConversationCloseReason.AccountBlockedOrSuspended, at.AddHours(1));
                break;
            case MatchKind.ClosedCompany:
                match.CloseConversation(null, ConversationCloseReason.CompanySuspended, at.AddHours(1));
                break;
            default:
                break;
        }

        await SaveAsync(factory, context => context.Matches.Add(match));
        return match.Id;
    }

    /// <summary>
    /// Grava uma mensagem diretamente na base de dados.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="matchId">Match da conversa.</param>
    /// <param name="senderId">Conta do remetente.</param>
    /// <param name="content">Texto da mensagem.</param>
    /// <param name="sentAt">Instante do envio.</param>
    /// <param name="readAt">Instante da leitura; <see langword="null"/> se por ler.</param>
    /// <returns>O identificador da mensagem.</returns>
    internal static async Task<Guid> SeedMessageAsync(
        KatchWebApplicationFactory factory,
        Guid matchId,
        Guid senderId,
        string content,
        DateTimeOffset sentAt,
        DateTimeOffset? readAt = null)
    {
        var message = Message.Send(matchId, senderId, content, sentAt);
        if (readAt is not null)
        {
            message.MarkRead(readAt.Value);
        }

        await SaveAsync(factory, context => context.Messages.Add(message));
        return message.Id;
    }

    /// <summary>
    /// Lê o match da base de dados, sem o acompanhar.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="matchId">Identificador do match.</param>
    /// <returns>O match.</returns>
    internal static async Task<Match> ReadMatchAsync(KatchWebApplicationFactory factory, Guid matchId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Matches.AsNoTracking().SingleAsync(match => match.Id == matchId, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Lê as mensagens de um match da base de dados, por ordem de envio.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="matchId">Identificador do match.</param>
    /// <returns>As mensagens.</returns>
    internal static async Task<List<Message>> ReadMessagesAsync(KatchWebApplicationFactory factory, Guid matchId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Messages.AsNoTracking()
            .Where(message => message.MatchId == matchId)
            .OrderBy(message => message.SentAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Envia um pedido à API, com a credencial de sessão se indicada.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="method">Método HTTP.</param>
    /// <param name="path">Caminho do pedido.</param>
    /// <param name="token">Credencial de sessão; <see langword="null"/> para omitir o cabeçalho.</param>
    /// <param name="body">Corpo JSON; <see langword="null"/> sem corpo.</param>
    /// <returns>A resposta, que quem chama liberta.</returns>
    internal static async Task<HttpResponseMessage> SendAsync(
        KatchWebApplicationFactory factory,
        HttpMethod method,
        string path,
        string? token,
        object? body = null)
    {
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string IssueToken(KatchWebApplicationFactory factory, AppUser user)
    {
        return factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
    }

    private static async Task SaveAsync(KatchWebApplicationFactory factory, Action<KatchDbContext> add)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        add(context);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Recrutador com a sua Empresa e a credencial de sessão.
    /// </summary>
    /// <param name="User">Conta do Recrutador.</param>
    /// <param name="Company">Empresa do Recrutador.</param>
    /// <param name="Location">Localidade da Empresa.</param>
    /// <param name="Token">Credencial de sessão.</param>
    internal sealed record CompanySeed(AppUser User, Company Company, Location Location, string Token);

    /// <summary>
    /// Candidato com a sua conta e a credencial de sessão.
    /// </summary>
    /// <param name="User">Conta do Candidato.</param>
    /// <param name="Candidate">Candidato.</param>
    /// <param name="Token">Credencial de sessão.</param>
    internal sealed record CandidateSeed(AppUser User, Candidate Candidate, string Token);
}
