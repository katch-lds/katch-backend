// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net.Http.Json;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Contas de teste com palavra-passe real e chamadas ao início de sessão, partilhadas pelos testes de
/// <c>/api/auth</c>. Os dados são fictícios.
/// </summary>
internal static class AuthTestAccounts
{
    /// <summary>
    /// Palavra-passe das contas semeadas, a do critério de aceitação do RF002.
    /// </summary>
    internal const string Password = "katch2026";

    /// <summary>
    /// Rota do início de sessão.
    /// </summary>
    internal const string LoginRoute = "/api/auth/login";

    private static long taxIdCounter = 600_000_000;

    /// <summary>
    /// Grava uma conta ativa com o resumo da palavra-passe calculado pelo <see cref="IPasswordHasher"/> da aplicação. Um
    /// Recrutador tem sempre a sua linha de <see cref="Recruiter"/> e, se o estado for indicado, a Empresa nesse estado.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="configure">Alteração opcional da conta antes de gravar (por exemplo, bloqueá-la).</param>
    /// <param name="companyStatus">Estado da Empresa do Recrutador; <see langword="null"/> para não registar Empresa.</param>
    /// <param name="password">Palavra-passe da conta.</param>
    /// <returns>A conta gravada.</returns>
    internal static async Task<AppUser> SeedAsync(
        KatchWebApplicationFactory factory,
        UserType userType,
        Action<AppUser>? configure = null,
        CompanyStatus? companyStatus = null,
        string password = Password)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        using IServiceScope scope = factory.Services.CreateScope();
        string hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(password);
        var user = new AppUser(
            $"{userType.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@exemplo.pt",
            hash,
            userType,
            now,
            now);
        configure?.Invoke(user);

        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        context.Users.Add(user);

        if (userType == UserType.Recruiter)
        {
            var recruiter = new Recruiter(user);
            context.Recruiters.Add(recruiter);

            if (companyStatus is not null)
            {
                var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
                context.Locations.Add(location);
                context.Companies.Add(NewCompany(recruiter.Id, location.Id, companyStatus.Value, now));
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    /// <summary>
    /// Envia <c>POST /api/auth/login</c>.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="body">Corpo JSON do pedido.</param>
    /// <param name="clientApp">Valor do cabeçalho <c>X-Client-App</c>, ou <see langword="null"/> para o omitir.</param>
    /// <param name="authorization">Valor do cabeçalho <c>Authorization</c>, ou <see langword="null"/> para o omitir.</param>
    /// <returns>A resposta, que quem chama liberta.</returns>
    internal static async Task<HttpResponseMessage> LoginAsync(
        KatchWebApplicationFactory factory,
        object? body,
        string? clientApp,
        string? authorization = null)
    {
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(LoginRoute, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        if (clientApp is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Client-App", clientApp);
        }

        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Corpo de um pedido de início de sessão.
    /// </summary>
    /// <param name="email">Correio eletrónico.</param>
    /// <param name="password">Palavra-passe.</param>
    /// <returns>O objeto a serializar.</returns>
    internal static object Credentials(string email, string password)
    {
        return new { email, password };
    }

    /// <summary>
    /// Lê o corpo JSON de uma resposta.
    /// </summary>
    /// <param name="response">Resposta recebida.</param>
    /// <returns>A raiz do documento, que sobrevive à libertação do mesmo.</returns>
    internal static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        using JsonDocument json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return json.RootElement.Clone();
    }

    /// <summary>
    /// Lê os erros de campo de uma resposta ERR-10.
    /// </summary>
    /// <param name="error">Corpo do erro.</param>
    /// <returns>Um par (campo, motivo) por erro de campo.</returns>
    internal static List<(string Field, string Reason)> Fields(JsonElement error)
    {
        return error.GetProperty("fields")
            .EnumerateArray()
            .Select(field => (field.GetProperty("field").GetString()!, field.GetProperty("reason").GetString()!))
            .ToList();
    }

    private static Company NewCompany(Guid recruiterId, Guid locationId, CompanyStatus status, DateTimeOffset now)
    {
        var registration = new CompanyRegistration
        {
            CompanyName = "Empresa Exemplo, Lda.",
            TaxId = Interlocked.Increment(ref taxIdCounter).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Industry = Industry.Technology,
            Address = "Rua de Exemplo, 1, 4700-000 Braga",
            LocationId = locationId,
            ContactEmail = "geral@empresa-exemplo.pt",
            ContactPhone = "253000000",
            ResponsibleName = "Responsável Exemplo",
        };
        var company = new Company(recruiterId, registration, now);
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

        return company;
    }
}
