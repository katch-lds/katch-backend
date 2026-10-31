// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes do detalhe da vaga (documentação da API v02, secção 6.3.2; RF016; UC05, A2), sobre SQLite in-memory. A base é
/// partilhada pelos testes da classe, por isso cada teste cria o seu Candidato, as suas localidades e as suas vagas.
/// </summary>
public sealed class JobDetailTests : IClassFixture<KatchWebApplicationFactory>
{
    private const decimal BragaLatitude = 41.545450m;
    private const decimal BragaLongitude = -8.426507m;

    private static long taxIdCounter = 760_000_000;

    private readonly KatchWebApplicationFactory factory;
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow.AddTicks(-(DateTimeOffset.UtcNow.Ticks % TimeSpan.TicksPerSecond));

    /// <summary>
    /// Initializes a new instance of the <see cref="JobDetailTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public JobDetailTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// O detalhe tem os cinco campos da API v02 (8.2): o cartão com os doze campos, a descrição completa, as designações
    /// das competências e dos benefícios por ordem alfabética e o identificador da Empresa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DetailHasTheDocumentedFields()
    {
        (_, string token) = await this.SeedCandidateAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string logo = $"company_logo/{Guid.NewGuid():N}.png";
        string photo = $"job_photo/{Guid.NewGuid():N}.jpg";
        (Guid jobId, Guid companyId) = await this.SeedJobAsync(
            22,
            WorkMode.Hybrid,
            isUrgent: true,
            description: "Desenvolvimento e manutenção de APIs REST em ASP.NET Core.",
            skills: [$"SQL {suffix}", $"C# {suffix}", $"Docker {suffix}"],
            benefits: [$"Seguro de saúde {suffix}", $"Ginásio {suffix}"],
            configure: job => job.AddPhoto(photo),
            configureCompany: company => company.SetLogo(logo));

        using HttpResponseMessage response = await this.GetDetailAsync(jobId, token);

        JsonElement detail = await ReadJsonAsync(response);
        Assert.Equal(
            ["benefits", "card", "companyId", "description", "skills"],
            detail.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        JsonElement card = detail.GetProperty("card");
        Assert.Equal(
            [
                "companyName", "contractType", "distanceKm", "isUrgent", "jobId", "locationName",
                "logoUrl", "maxSalary", "minSalary", "photoUrls", "title", "workMode",
            ],
            card.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(jobId, card.GetProperty("jobId").GetGuid());
        Assert.Equal("Programador Backend .NET", card.GetProperty("title").GetString());
        Assert.Equal("Lumen Software, Lda.", card.GetProperty("companyName").GetString());
        Assert.Equal("/api/files/COMPANY_LOGO/" + logo.Split('/')[1], card.GetProperty("logoUrl").GetString());
        Assert.Equal(1600.00m, card.GetProperty("minSalary").GetDecimal());
        Assert.Equal(2100.00m, card.GetProperty("maxSalary").GetDecimal());
        Assert.EndsWith("(Braga)", card.GetProperty("locationName").GetString(), StringComparison.Ordinal);
        Assert.Equal(22, card.GetProperty("distanceKm").GetInt32());
        Assert.Equal("PERMANENT", card.GetProperty("contractType").GetString());
        Assert.Equal("HYBRID", card.GetProperty("workMode").GetString());
        Assert.True(card.GetProperty("isUrgent").GetBoolean());
        Assert.Equal(
            ["/api/files/JOB_PHOTO/" + photo.Split('/')[1]],
            card.GetProperty("photoUrls").EnumerateArray().Select(url => url.GetString()));
        Assert.Equal("Desenvolvimento e manutenção de APIs REST em ASP.NET Core.", detail.GetProperty("description").GetString());
        Assert.Equal(
            [$"C# {suffix}", $"Docker {suffix}", $"SQL {suffix}"],
            detail.GetProperty("skills").EnumerateArray().Select(skill => skill.GetString()));
        Assert.Equal(
            [$"Ginásio {suffix}", $"Seguro de saúde {suffix}"],
            detail.GetProperty("benefits").EnumerateArray().Select(benefit => benefit.GetString()));
        Assert.Equal(companyId, detail.GetProperty("companyId").GetGuid());
    }

    /// <summary>
    /// O cartão do detalhe é igual ao cartão de vaga seguinte da mesma vaga, incluindo a distância (RF015).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DetailCardIsTheSameAsTheNextCard()
    {
        (_, string token) = await this.SeedCandidateAsync(candidate => candidate.MaxDistanceKm = 50);
        (Guid jobId, _) = await this.SeedJobAsync(37, WorkMode.OnSite, isUrgent: true);

        JsonElement? next = null;
        for (int step = 0; step < 1000 && next is null; step++)
        {
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "next", token);
            JsonElement card = await ReadJsonAsync(response);
            if (card.GetProperty("jobId").GetGuid() == jobId)
            {
                next = card;
            }
            else
            {
                using HttpResponseMessage decline = await this.SendAsync(
                    HttpMethod.Post,
                    $"{card.GetProperty("jobId").GetGuid()}/decline",
                    token);
                Assert.Equal(HttpStatusCode.NoContent, decline.StatusCode);
            }
        }

        using HttpResponseMessage detail = await this.GetDetailAsync(jobId, token);

        JsonElement detailCard = (await ReadJsonAsync(detail)).GetProperty("card");
        Assert.NotNull(next);
        Assert.Equal(next.Value.GetRawText(), detailCard.GetRawText());
        Assert.Equal(37, detailCard.GetProperty("distanceKm").GetInt32());
    }

    /// <summary>
    /// O detalhe não depende das preferências de procura nem das respostas: uma vaga presencial a 30 km, abaixo da
    /// pretensão mínima, tem detalhe para um Candidato com distância máxima de 5 km, antes e depois de a recusar, e uma vaga
    /// com interesse também.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DetailIgnoresThePreferencesAndTheAnswers()
    {
        (_, string token) = await this.SeedCandidateAsync(candidate =>
        {
            candidate.MaxDistanceKm = 5;
            candidate.MinSalaryExpectation = 5000m;
            candidate.PreferredWorkModes = [WorkMode.Remote];
            candidate.PreferredContractTypes = [ContractType.Internship];
        });
        (Guid declined, _) = await this.SeedJobAsync(30, WorkMode.OnSite);
        (Guid interest, _) = await this.SeedJobAsync(30, WorkMode.OnSite);

        using (HttpResponseMessage before = await this.GetDetailAsync(declined, token))
        {
            Assert.Equal(30, (await ReadJsonAsync(before)).GetProperty("card").GetProperty("distanceKm").GetInt32());
        }

        using (HttpResponseMessage answer = await this.SendAsync(HttpMethod.Post, $"{declined}/decline", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, answer.StatusCode);
        }

        using (HttpResponseMessage answer = await this.SendAsync(HttpMethod.Post, $"{interest}/interest", token))
        {
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        }

        using HttpResponseMessage afterDecline = await this.GetDetailAsync(declined, token);
        using HttpResponseMessage afterInterest = await this.GetDetailAsync(interest, token);
        Assert.Equal(HttpStatusCode.OK, afterDecline.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterInterest.StatusCode);
    }

    /// <summary>
    /// Uma vaga inexistente, eliminada, por publicar, suspensa, encerrada ou expirada (ainda publicada, antes da tarefa
    /// periódica), ou de uma Empresa pendente ou suspensa, responde 404 ERR-20.
    /// </summary>
    /// <param name="state">Estado da vaga ou da Empresa.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("inexistente")]
    [InlineData("eliminada")]
    [InlineData("por publicar")]
    [InlineData("suspensa")]
    [InlineData("encerrada")]
    [InlineData("expirada")]
    [InlineData("Empresa pendente")]
    [InlineData("Empresa suspensa")]
    public async Task DetailOfAJobNotAvailableToCandidatesReturnsErr20(string state)
    {
        (_, string token) = await this.SeedCandidateAsync();
        Guid jobId = state switch
        {
            "inexistente" => Guid.NewGuid(),
            "eliminada" => (await this.SeedJobAsync(5, WorkMode.OnSite, configure: job => job.MarkDeleted(this.now.AddDays(-1)))).JobId,
            "por publicar" => (await this.SeedJobAsync(5, WorkMode.OnSite, publish: false)).JobId,
            "suspensa" => (await this.SeedJobAsync(5, WorkMode.OnSite, configure: job => job.Suspend(this.now.AddDays(-1)))).JobId,
            "encerrada" => (await this.SeedJobAsync(5, WorkMode.OnSite, configure: job => job.Close())).JobId,
            "expirada" => (await this.SeedJobAsync(5, WorkMode.OnSite, expiresAt: this.now.AddMinutes(-1))).JobId,
            "Empresa pendente" => (await this.SeedJobAsync(5, WorkMode.OnSite, company: CompanyStatus.Pending)).JobId,
            _ => (await this.SeedJobAsync(5, WorkMode.OnSite, company: CompanyStatus.Suspended)).JobId,
        };

        using HttpResponseMessage response = await this.GetDetailAsync(jobId, token);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.NotFound,
            "ERR-20",
            "O elemento pedido não existe ou não está disponível.");
    }

    /// <summary>
    /// A rota do detalhe só aceita um GUID: um identificador ilegível responde 404, e <c>next</c> e <c>quota</c> continuam
    /// a ser as suas operações.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DetailRouteOnlyAcceptsAGuid()
    {
        (_, string token) = await this.SeedCandidateAsync();

        using HttpResponseMessage unreadable = await this.SendAsync(HttpMethod.Get, "nao-e-um-guid", token);
        using HttpResponseMessage quota = await this.SendAsync(HttpMethod.Get, "quota", token);

        Assert.Equal(HttpStatusCode.NotFound, unreadable.StatusCode);
        JsonElement quotaBody = await ReadJsonAsync(quota);
        Assert.Equal(Candidate.MaxInterests, quotaBody.GetProperty("total").GetInt32());
    }

    /// <summary>
    /// O Recrutador e o Administrador recebem 403 ERR-06; sem credencial, 401 ERR-01.
    /// </summary>
    /// <param name="userType">Tipo de conta, ou <see langword="null"/> para o pedido sem credencial.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Recruiter)]
    [InlineData(UserType.Admin)]
    [InlineData(null)]
    public async Task OnlyTheCandidateGetsTheDetail(UserType? userType)
    {
        (Guid jobId, _) = await this.SeedJobAsync(5, WorkMode.OnSite);
        string? token = null;
        if (userType is { } type)
        {
            AppUser user = await AuthTestAccounts.SeedAsync(this.factory, type);
            token = this.IssueToken(user);
        }

        using HttpResponseMessage response = await this.GetDetailAsync(jobId, token);

        if (token is null)
        {
            await AuthenticationTests.AssertErrorAsync(
                response,
                HttpStatusCode.Unauthorized,
                "ERR-01",
                "A sessão expirou ou não é válida. Inicie sessão novamente.");
        }
        else
        {
            await AuthenticationTests.AssertErrorAsync(
                response,
                HttpStatusCode.Forbidden,
                "ERR-06",
                "A operação não está disponível para este tipo de conta.");
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(body);
        return json.RootElement.Clone();
    }

    private static CompanyRegistration Registration(Guid locationId)
    {
        return new CompanyRegistration
        {
            CompanyName = "Lumen Software, Lda.",
            TaxId = Interlocked.Increment(ref taxIdCounter).ToString(CultureInfo.InvariantCulture),
            Industry = Industry.Technology,
            Address = "Rua de Exemplo, 1, 4700-000 Braga",
            LocationId = locationId,
            ContactEmail = "geral@empresa-exemplo.pt",
            ContactPhone = "253000000",
            ResponsibleName = "Responsável Exemplo",
        };
    }

    /// <summary>
    /// Localidade a uma distância exata a norte de Braga: ao longo do meridiano, a distância de Haversine é R·Δφ.
    /// </summary>
    /// <param name="kilometres">Distância a Braga, em km.</param>
    /// <returns>A localidade, por gravar.</returns>
    private static Location LocationNorthOfBraga(double kilometres)
    {
        decimal latitude = Math.Round(
            BragaLatitude + (decimal)(kilometres / (HaversineDistanceCalculator.EarthRadiusKm * Math.PI / 180)),
            6);
        return new Location($"Vaga {Guid.NewGuid():N}", "Braga", latitude, BragaLongitude);
    }

    private async Task<(Candidate Candidate, string Token)> SeedCandidateAsync(Action<Candidate>? configure = null)
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Candidate, created, created);
        var candidate = new Candidate(user, "Ana Silva", "912345678", Guid.NewGuid());
        configure?.Invoke(candidate);

        await this.SaveAsync(context =>
        {
            var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", BragaLatitude, BragaLongitude);
            context.Locations.Add(location);
            candidate.LocationId = location.Id;
            context.Candidates.Add(candidate);
        });
        return (candidate, this.IssueToken(user));
    }

    private async Task<(Guid JobId, Guid CompanyId)> SeedJobAsync(
        double kilometresNorth,
        WorkMode workMode,
        bool isUrgent = false,
        CompanyStatus company = CompanyStatus.Approved,
        DateTimeOffset? expiresAt = null,
        bool publish = true,
        string description = "Descrição da vaga.",
        IReadOnlyList<string>? skills = null,
        IReadOnlyList<string>? benefits = null,
        Action<Job>? configure = null,
        Action<Company>? configureCompany = null)
    {
        Location location = LocationNorthOfBraga(kilometresNorth);
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Recruiter, created, created);
        var recruiter = new Recruiter(user);
        var companyEntity = new Company(recruiter.Id, Registration(location.Id), created);
        if (company is CompanyStatus.Approved or CompanyStatus.Suspended)
        {
            companyEntity.Approve();
            configureCompany?.Invoke(companyEntity);
        }

        if (company == CompanyStatus.Suspended)
        {
            companyEntity.Suspend();
        }

        var job = new Job(
            companyEntity.Id,
            new JobData
            {
                Title = "Programador Backend .NET",
                Description = description,
                MinSalary = 1600.00m,
                MaxSalary = 2100.00m,
                LocationId = location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = workMode,
                IsUrgent = isUrgent,
                ExpiresAt = expiresAt,
            });
        if (publish)
        {
            job.Publish(this.now.AddDays(-10));
        }

        foreach (string skill in skills ?? [])
        {
            job.Skills.Add(new Skill(skill));
        }

        foreach (string benefit in benefits ?? [])
        {
            job.Benefits.Add(new Benefit(benefit));
        }

        configure?.Invoke(job);

        await this.SaveAsync(context =>
        {
            context.Locations.Add(location);
            context.Recruiters.Add(recruiter);
            context.Companies.Add(companyEntity);
            context.Jobs.Add(job);
        });
        return (job.Id, companyEntity.Id);
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

    private Task<HttpResponseMessage> GetDetailAsync(Guid jobId, string? token)
    {
        return this.SendAsync(HttpMethod.Get, jobId.ToString(), token);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, string? token)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri($"/api/candidate/jobs/{relativePath}", UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
