// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Headers;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes da política <c>ApprovedCompany</c> e do ERR-07, sobre o endpoint <c>approved-company</c> de
/// <see cref="AuthorizationProbeController"/>.
/// </summary>
public sealed class ApprovedCompanyTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Endpoint = "approved-company";

    private static long taxIdCounter = 500_000_000;

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovedCompanyTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public ApprovedCompanyTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Um Recrutador com a Empresa aprovada passa a política.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RecruiterWithApprovedCompanyReturnsOk()
    {
        AppUser user = await this.SeedRecruiterAsync(CompanyStatus.Approved);

        using HttpResponseMessage response = await this.GetAsync(this.IssueToken(user));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Um Recrutador com a Empresa noutro estado, ou sem Empresa, recebe o ERR-07 com o estado na mensagem.
    /// </summary>
    /// <param name="status">Estado da Empresa, ou <see langword="null"/> se o Recrutador não tem Empresa.</param>
    /// <param name="expectedMessage">Mensagem esperada.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending, "A operação exige a Empresa aprovada. Estado atual da Empresa: pendente.")]
    [InlineData(CompanyStatus.Rejected, "A operação exige a Empresa aprovada. Estado atual da Empresa: recusada.")]
    [InlineData(CompanyStatus.Suspended, "A operação exige a Empresa aprovada. Estado atual da Empresa: suspensa.")]
    [InlineData(null, "A operação exige a Empresa aprovada. Estado atual da Empresa: não registada.")]
    public async Task RecruiterWithoutApprovedCompanyReturnsErr07(CompanyStatus? status, string expectedMessage)
    {
        AppUser user = await this.SeedRecruiterAsync(status);

        using HttpResponseMessage response = await this.GetAsync(this.IssueToken(user));

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-07", expectedMessage);
    }

    /// <summary>
    /// Um Candidato ou um Administrador recebe o ERR-06 e nunca o ERR-07.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Admin)]
    public async Task NonRecruiterReturnsErr06(UserType userType)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", userType, now, now);
        await this.SaveAsync(context => context.Users.Add(user));

        using HttpResponseMessage response = await this.GetAsync(this.IssueToken(user));

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Forbidden,
            "ERR-06",
            "A operação não está disponível para este tipo de conta.");
    }

    /// <summary>
    /// A suspensão da Empresa produz efeito no pedido seguinte, sem esperar pela expiração da credencial.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SuspendingTheCompanyAfterIssuingTheTokenTakesEffectOnTheNextRequest()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AppUser user = await this.SeedRecruiterAsync(CompanyStatus.Approved);
        string token = this.IssueToken(user);

        using HttpResponseMessage before = await this.GetAsync(token);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Company company = await context.Companies.SingleAsync(item => item.RecruiterId == user.Id, cancellationToken);
            company.Suspend();
            await context.SaveChangesAsync(cancellationToken);
        }

        using HttpResponseMessage after = await this.GetAsync(token);

        await AuthenticationTests.AssertErrorAsync(
            after,
            HttpStatusCode.Forbidden,
            "ERR-07",
            "A operação exige a Empresa aprovada. Estado atual da Empresa: suspensa.");
    }

    /// <summary>
    /// O estado da conta vem antes da Empresa: um Recrutador bloqueado com a Empresa aprovada recebe o ERR-03.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BlockedRecruiterWithApprovedCompanyReturnsErr03()
    {
        AppUser user = await this.SeedRecruiterAsync(CompanyStatus.Approved, blocked: true);

        using HttpResponseMessage response = await this.GetAsync(this.IssueToken(user));

        await AuthenticationTests.AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-03", "A conta está bloqueada.");
    }

    /// <summary>
    /// Sem credencial, ou com uma credencial inválida, o pedido é recusado com o ERR-01.
    /// </summary>
    /// <param name="token">Credencial enviada, ou <see langword="null"/> se não houver.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("abc.def.ghi")]
    public async Task MissingOrInvalidTokenReturnsErr01(string? token)
    {
        using HttpResponseMessage response = await this.GetAsync(token);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            "ERR-01",
            "A sessão expirou ou não é válida. Inicie sessão novamente.");
    }

    private static CompanyRegistration Registration(Guid locationId)
    {
        return new CompanyRegistration
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
    }

    private static Company NewCompany(Guid recruiterId, Guid locationId, CompanyStatus status, DateTimeOffset now)
    {
        var company = new Company(recruiterId, Registration(locationId), now);
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

    private async Task<AppUser> SeedRecruiterAsync(CompanyStatus? companyStatus, bool blocked = false)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Recruiter, now, now);
        if (blocked)
        {
            user.Block(now);
        }

        var recruiter = new Recruiter(user);
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);

        await this.SaveAsync(context =>
        {
            context.Recruiters.Add(recruiter);
            context.Locations.Add(location);
            if (companyStatus is not null)
            {
                context.Companies.Add(NewCompany(recruiter.Id, location.Id, companyStatus.Value, now));
            }
        });
        return user;
    }

    private async Task SaveAsync(Action<KatchDbContext> add)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        add(context);
        await context.SaveChangesAsync(cancellationToken);
    }

    private string IssueToken(AppUser user)
    {
        return this.factory.Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
    }

    private async Task<HttpResponseMessage> GetAsync(string? token)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/test-authorization/{Endpoint}", UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
