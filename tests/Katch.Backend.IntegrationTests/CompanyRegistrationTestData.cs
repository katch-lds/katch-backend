// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Dados e pedidos dos testes da aprovação e da recusa do registo das Empresas (documentação da API v02, secções
/// 6.11.1 a 6.11.4). As credenciais são emitidas pelo <see cref="ITokenService"/> da aplicação.
/// </summary>
internal static class CompanyRegistrationTestData
{
    private static long taxIdCounter = 800_000_000;

    /// <summary>
    /// Cria uma conta de Administrador e devolve-a com a credencial.
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
    /// Cria um Candidato e devolve a credencial.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <returns>A credencial do Candidato.</returns>
    internal static async Task<string> SeedCandidateTokenAsync(KatchWebApplicationFactory factory)
    {
        AppUser user = NewUser(factory, UserType.Candidate, $"candidato-{Guid.NewGuid():N}@exemplo.pt");
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        await SaveAsync(factory, context =>
        {
            context.Locations.Add(location);
            context.Candidates.Add(new Candidate(user, "Ana Ferreira", "912345678", location.Id));
        });
        return IssueToken(factory, user);
    }

    /// <summary>
    /// Cria um Recrutador e a sua Empresa no estado indicado, numa localidade nova, e devolve-os com a credencial do
    /// Recrutador.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="status">Estado da Empresa.</param>
    /// <param name="companyName">Designação social.</param>
    /// <param name="submittedAt">Instante da submissão, ou <see langword="null"/> para o instante atual.</param>
    /// <returns>A Empresa, a localidade, a conta do Recrutador e a credencial.</returns>
    internal static async Task<(Company Company, Location Location, AppUser Recruiter, string Token)> SeedCompanyAsync(
        KatchWebApplicationFactory factory,
        CompanyStatus status,
        string companyName = "Lumen Software, Lda.",
        DateTimeOffset? submittedAt = null)
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
        var company = new Company(recruiter.Id, registration, submittedAt ?? DateTimeOffset.UtcNow);
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
        return (company, location, user, IssueToken(factory, user));
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
    /// Envia um pedido à aplicação, com a credencial e o corpo JSON indicados.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="method">Método HTTP.</param>
    /// <param name="path">Rota absoluta.</param>
    /// <param name="token">Credencial, ou <see langword="null"/> para um pedido sem sessão.</param>
    /// <param name="body">Corpo JSON, ou <see langword="null"/> para um pedido sem corpo.</param>
    /// <returns>A resposta.</returns>
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

    /// <summary>
    /// Confirma o código de estado, o identificador do erro e a mensagem de uma resposta de erro.
    /// </summary>
    /// <param name="response">Resposta.</param>
    /// <param name="status">Código de estado esperado.</param>
    /// <param name="code">Identificador do erro esperado.</param>
    /// <param name="message">Mensagem esperada.</param>
    /// <returns>O corpo do erro.</returns>
    internal static async Task<JsonElement> AssertErrorAsync(
        HttpResponseMessage response,
        System.Net.HttpStatusCode status,
        string code,
        string message)
    {
        Assert.Equal(status, response.StatusCode);
        JsonElement error = await AuthTestAccounts.ReadJsonAsync(response);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(message, error.GetProperty("message").GetString());
        return error;
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
