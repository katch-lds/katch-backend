// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Dados e pedidos dos testes da administração das contas e das Empresas (documentação da API v02, secções 6.10 e
/// 6.11.5 a 6.11.8). As contas são criadas com a palavra-passe de <see cref="AuthTestAccounts.Password"/>, para os
/// testes do início de sessão, e as credenciais são emitidas pelo <see cref="ITokenService"/> da aplicação.
/// </summary>
internal static class AdminTestData
{
    private static long taxIdCounter = 700_000_000;

    /// <summary>
    /// Cria uma conta de Administrador e devolve a credencial.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <returns>A conta e a credencial.</returns>
    internal static async Task<(AppUser User, string Token)> SeedAdminAsync(KatchWebApplicationFactory factory)
    {
        AppUser user = NewUser(factory, UserType.Admin, $"admin-{Guid.NewGuid():N}@katch.pt");
        await SaveAsync(factory, context => context.Admins.Add(new Admin(user)));
        return (user, IssueToken(factory, user));
    }

    /// <summary>
    /// Cria um Candidato numa localidade nova e devolve-o com a credencial.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="fullName">Nome do Candidato.</param>
    /// <param name="locationName">Nome da localidade, sem o distrito.</param>
    /// <returns>O Candidato e a credencial.</returns>
    internal static async Task<(Candidate Candidate, string Token)> SeedCandidateAsync(
        KatchWebApplicationFactory factory,
        string fullName = "Ana Ferreira",
        string? locationName = null)
    {
        AppUser user = NewUser(factory, UserType.Candidate, $"candidato-{Guid.NewGuid():N}@exemplo.pt");
        var location = new Location(locationName ?? $"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        var candidate = new Candidate(user, fullName, "912345678", location.Id);
        await SaveAsync(factory, context =>
        {
            context.Locations.Add(location);
            context.Candidates.Add(candidate);
        });
        return (candidate, IssueToken(factory, user));
    }

    /// <summary>
    /// Cria um Recrutador e a sua Empresa no estado indicado e devolve-os com a credencial do Recrutador.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="status">Estado da Empresa.</param>
    /// <param name="companyName">Designação social.</param>
    /// <returns>A Empresa, a conta do Recrutador e a credencial.</returns>
    internal static async Task<(Company Company, AppUser Recruiter, string Token)> SeedCompanyAsync(
        KatchWebApplicationFactory factory,
        CompanyStatus status,
        string companyName = "Lumen Software, Lda.")
    {
        AppUser user = NewUser(factory, UserType.Recruiter, $"recrutador-{Guid.NewGuid():N}@exemplo.pt");
        var recruiter = new Recruiter(user);
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        var registration = new CompanyRegistration
        {
            CompanyName = companyName,
            TaxId = Interlocked.Increment(ref taxIdCounter).ToString(CultureInfo.InvariantCulture),
            Industry = Industry.Technology,
            Address = "Avenida da Liberdade, 100, 4710-000 Braga",
            LocationId = location.Id,
            ContactEmail = "geral@lumen-exemplo.pt",
            ContactPhone = "253000111",
            ResponsibleName = "Rui Matos",
        };
        var company = new Company(recruiter.Id, registration, DateTimeOffset.UtcNow);
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

        await SaveAsync(factory, context =>
        {
            context.Recruiters.Add(recruiter);
            context.Locations.Add(location);
            context.Companies.Add(company);
        });
        return (company, user, IssueToken(factory, user));
    }

    /// <summary>
    /// Cria uma vaga remota da Empresa, publicada no instante indicado, ou por publicar.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="companyId">Empresa da vaga.</param>
    /// <param name="title">Função da vaga.</param>
    /// <param name="publishedAt">Instante da publicação, ou <see langword="null"/> para a deixar por publicar.</param>
    /// <param name="configure">Alteração aplicada depois da publicação, por exemplo a suspensão.</param>
    /// <returns>A vaga.</returns>
    internal static async Task<Job> SeedJobAsync(
        KatchWebApplicationFactory factory,
        Guid companyId,
        string title,
        DateTimeOffset? publishedAt,
        Action<Job>? configure = null)
    {
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        var job = new Job(
            companyId,
            new JobData
            {
                Title = title,
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        if (publishedAt is not null)
        {
            job.Publish(publishedAt.Value);
        }

        configure?.Invoke(job);
        await SaveAsync(factory, context =>
        {
            context.Locations.Add(location);
            context.Jobs.Add(job);
        });
        return job;
    }

    /// <summary>
    /// Regista a resposta de um Candidato a uma vaga: recusa, interesse em espera ou match com a conversa aberta.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="candidateId">Candidato.</param>
    /// <param name="jobId">Vaga.</param>
    /// <param name="status">Estado da resposta.</param>
    /// <param name="recruiterId">Recrutador que aceita, no estado <see cref="MatchStatus.Matched"/>.</param>
    /// <returns>A resposta.</returns>
    internal static async Task<Match> SeedMatchAsync(
        KatchWebApplicationFactory factory,
        Guid candidateId,
        Guid jobId,
        MatchStatus status,
        Guid? recruiterId = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Match match = status == MatchStatus.DeclinedByCandidate
            ? Match.Decline(candidateId, jobId, now)
            : Match.ExpressInterest(candidateId, jobId, now);
        if (status == MatchStatus.Matched)
        {
            match.OpenProfile(now);
            match.Accept(recruiterId ?? Guid.NewGuid(), now);
        }

        await SaveAsync(factory, context => context.Matches.Add(match));
        return match;
    }

    /// <summary>
    /// Lê uma resposta de um Candidato tal como está na base de dados.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="matchId">Identificador da resposta.</param>
    /// <returns>A resposta.</returns>
    internal static async Task<Match> ReadMatchAsync(KatchWebApplicationFactory factory, Guid matchId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Matches.AsNoTracking()
            .SingleAsync(match => match.Id == matchId, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Lê as operações registadas sobre um elemento, pela ordem do registo (RF112).
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="entityId">Identificador do elemento.</param>
    /// <returns>O autor e o tipo de cada operação.</returns>
    internal static async Task<List<(Guid? UserId, OperationType Operation)>> ReadOperationsAsync(
        KatchWebApplicationFactory factory,
        Guid entityId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        List<OperationLog> entries = await context.OperationLogs.AsNoTracking()
            .Where(entry => entry.EntityId == entityId)
            .ToListAsync(TestContext.Current.CancellationToken);
        return entries.OrderBy(entry => entry.Id).Select(entry => (entry.UserId, entry.Operation)).ToList();
    }

    /// <summary>
    /// Envia um pedido à aplicação, com a credencial indicada.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="method">Método HTTP.</param>
    /// <param name="path">Rota absoluta.</param>
    /// <param name="token">Credencial, ou <see langword="null"/> para um pedido sem sessão.</param>
    /// <returns>A resposta.</returns>
    internal static async Task<HttpResponseMessage> SendAsync(
        KatchWebApplicationFactory factory,
        HttpMethod method,
        string path,
        string? token)
    {
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Confirma o código de estado, o identificador do erro e a mensagem de uma resposta de erro.
    /// </summary>
    /// <param name="response">Resposta.</param>
    /// <param name="status">Código de estado esperado.</param>
    /// <param name="code">Identificador do erro esperado.</param>
    /// <param name="message">Mensagem esperada.</param>
    /// <returns>Tarefa da verificação.</returns>
    internal static async Task AssertErrorAsync(
        HttpResponseMessage response,
        System.Net.HttpStatusCode status,
        string code,
        string message)
    {
        Assert.Equal(status, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(message, error.GetProperty("message").GetString());
    }

    private static AppUser NewUser(KatchWebApplicationFactory factory, UserType userType, string email)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        string hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(AuthTestAccounts.Password);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        return new AppUser(email, hash, userType, userType == UserType.Admin ? null : now, now);
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
}
