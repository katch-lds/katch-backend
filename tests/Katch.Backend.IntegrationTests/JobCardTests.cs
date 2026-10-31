// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Diagnostics;
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
/// Testes do cartão de vaga seguinte (documentação da API v02, secção 6.3.1; RF013 a RF015), sobre SQLite in-memory. A
/// base é partilhada pelos testes da classe, por isso cada teste cria o seu Candidato e as suas vagas e percorre os cartões
/// até ao fim (pedindo o cartão e recusando-o), para verificar quais das suas vagas aparecem. A recusa só altera as
/// respostas desse Candidato.
/// </summary>
public sealed class JobCardTests : IClassFixture<KatchWebApplicationFactory>
{
    /// <summary>
    /// Raio médio da Terra em km, o mesmo da consulta (<c>JobRepository</c>).
    /// </summary>
    private const double EarthRadiusKm = 6371.0088;

    private const decimal BragaLatitude = 41.545450m;
    private const decimal BragaLongitude = -8.426507m;

    private static long taxIdCounter = 700_000_000;

    private readonly KatchWebApplicationFactory factory;
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow.AddTicks(-(DateTimeOffset.UtcNow.Ticks % TimeSpan.TicksPerSecond));

    /// <summary>
    /// Initializes a new instance of the <see cref="JobCardTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public JobCardTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;

        // A fábrica é partilhada: cada teste começa com o relógio do sistema e fixa o instante depois de emitir a credencial.
        factory.Clock.Fixed = null;
    }

    /// <summary>
    /// Exemplo do critério de aceitação do RF014, com distância máxima de 20 km e pretensão mínima de 1200 euros: uma vaga
    /// presencial a 30 km, uma vaga de 900 a 1100 euros, uma vaga suspensa, uma vaga de Empresa suspensa e uma vaga já
    /// recusada não aparecem, e uma vaga remota a 300 km aparece. Uma vaga com interesse e uma híbrida a 30 km também não
    /// aparecem.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task Rf014AcceptanceExampleShowsOnlyTheEligibleJobs()
    {
        (_, string token) = await this.SeedCandidateAsync(candidate =>
        {
            candidate.MaxDistanceKm = 20;
            candidate.MinSalaryExpectation = 1200m;
        });

        Guid near = await this.SeedJobAsync(5, WorkMode.OnSite, minSalary: 1500m, maxSalary: 2000m);
        Guid onSiteAt30 = await this.SeedJobAsync(30, WorkMode.OnSite);
        Guid hybridAt30 = await this.SeedJobAsync(30, WorkMode.Hybrid);
        Guid remoteAt300 = await this.SeedJobAsync(300, WorkMode.Remote);
        Guid lowSalary = await this.SeedJobAsync(5, WorkMode.OnSite, minSalary: 900m, maxSalary: 1100m);
        Guid suspended = await this.SeedJobAsync(5, WorkMode.OnSite, configure: job => job.Suspend(this.now.AddDays(-1)));
        Guid suspendedCompany = await this.SeedJobAsync(5, WorkMode.OnSite, company: CompanyStatus.Suspended);
        Guid declined = await this.SeedJobAsync(5, WorkMode.OnSite);
        Guid interest = await this.SeedJobAsync(5, WorkMode.OnSite);

        using (HttpResponseMessage answer = await this.SendAsync(HttpMethod.Post, $"{declined}/decline", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, answer.StatusCode);
        }

        using (HttpResponseMessage answer = await this.SendAsync(HttpMethod.Post, $"{interest}/interest", token))
        {
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        }

        List<JsonElement> cards = await this.WalkAsync(token);

        Guid[] mine = [near, onSiteAt30, hybridAt30, remoteAt300, lowSalary, suspended, suspendedCompany, declined, interest];
        Assert.Equal(new[] { near, remoteAt300 }.Order(), JobIds(cards).Where(id => mine.Contains(id)).Order());
        Assert.Equal(300, cards.Single(card => JobId(card) == remoteAt300).GetProperty("distanceKm").GetInt32());
        Assert.Equal(5, cards.Single(card => JobId(card) == near).GetProperty("distanceKm").GetInt32());
    }

    /// <summary>
    /// O regime de trabalho e o tipo de contrato fora das preferências excluem a vaga (RF014); as vagas remotas também
    /// são excluídas se o regime remoto não estiver nas preferências.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WorkModeAndContractTypeOutsideThePreferencesExcludeTheJob()
    {
        (_, string token) = await this.SeedCandidateAsync(candidate =>
        {
            candidate.PreferredWorkModes = [WorkMode.Hybrid, WorkMode.OnSite];
            candidate.PreferredContractTypes = [ContractType.Permanent];
        });

        Guid hybridPermanent = await this.SeedJobAsync(10, WorkMode.Hybrid);
        Guid onSitePermanent = await this.SeedJobAsync(10, WorkMode.OnSite);
        Guid remotePermanent = await this.SeedJobAsync(10, WorkMode.Remote);
        Guid hybridFixedTerm = await this.SeedJobAsync(10, WorkMode.Hybrid, contractType: ContractType.FixedTerm);
        Guid onSiteInternship = await this.SeedJobAsync(10, WorkMode.OnSite, contractType: ContractType.Internship);

        List<JsonElement> cards = await this.WalkAsync(token);

        Guid[] mine = [hybridPermanent, onSitePermanent, remotePermanent, hybridFixedTerm, onSiteInternship];
        Assert.Equal(new[] { hybridPermanent, onSitePermanent }.Order(), JobIds(cards).Where(id => mine.Contains(id)).Order());
    }

    /// <summary>
    /// Sem preferências de procura (valores nulos e listas vazias, DM-11), só se aplicam os filtros do estado: vaga
    /// publicada e não expirada de Empresa aprovada, sem resposta do Candidato. Ficam de fora a vaga por publicar, a
    /// encerrada, a de Empresa pendente e a que atingiu a data-limite (mesmo antes de a tarefa periódica a encerrar, N2).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WithoutPreferencesOnlyTheStateFiltersApply()
    {
        (_, string token) = await this.SeedCandidateAsync();

        Guid farAndCheap = await this.SeedJobAsync(400, WorkMode.OnSite, minSalary: 500m, maxSalary: 700m);
        Guid notExpiredYet = await this.SeedJobAsync(10, WorkMode.OnSite, expiresAt: this.now.AddDays(5));
        Guid expired = await this.SeedJobAsync(10, WorkMode.OnSite, expiresAt: this.now.AddDays(1));
        Guid draft = await this.SeedJobAsync(10, WorkMode.OnSite, publish: false);
        Guid closed = await this.SeedJobAsync(10, WorkMode.OnSite, configure: job => job.Close());
        Guid pendingCompany = await this.SeedJobAsync(10, WorkMode.OnSite, company: CompanyStatus.Pending);

        // Dois dias depois: a vaga com data-limite a um dia já expirou, a de cinco dias ainda não.
        this.factory.Clock.Fixed = this.now.AddDays(2);
        List<JsonElement> cards = await this.WalkAsync(token);

        Guid[] mine = [farAndCheap, notExpiredYet, expired, draft, closed, pendingCompany];
        Assert.Equal(new[] { farAndCheap, notExpiredYet }.Order(), JobIds(cards).Where(id => mine.Contains(id)).Order());
    }

    /// <summary>
    /// A distância do cartão coincide, com diferença máxima de 1 km, com a distância em linha reta calculada a partir das
    /// coordenadas das duas localidades (RF015), e é um número inteiro de km.
    /// </summary>
    /// <param name="name">Localidade da vaga.</param>
    /// <param name="latitude">Latitude da localidade da vaga, em graus.</param>
    /// <param name="longitude">Longitude da localidade da vaga, em graus.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("Porto", 41.149610, -8.611031)]
    [InlineData("Lisboa", 38.722252, -9.139337)]
    [InlineData("Faro", 37.019356, -7.930440)]
    [InlineData("Bragança", 41.806114, -6.756738)]
    public async Task DistanceMatchesTheStraightLineDistanceWithin1Km(string name, double latitude, double longitude)
    {
        (_, string token) = await this.SeedCandidateAsync();
        var location = new Location(
            $"{name} {Guid.NewGuid():N}",
            name,
            (decimal)latitude,
            (decimal)longitude);
        await this.SaveAsync(context => context.Locations.Add(location));
        Guid job = await this.SeedJobAsync(location, WorkMode.Remote);

        List<JsonElement> cards = await this.WalkAsync(token);

        JsonElement card = cards.Single(item => JobId(item) == job);
        double expected = Haversine(
            (double)BragaLatitude,
            (double)BragaLongitude,
            (double)location.Latitude,
            (double)location.Longitude);
        Assert.Equal(JsonValueKind.Number, card.GetProperty("distanceKm").ValueKind);
        Assert.True(
            Math.Abs(card.GetProperty("distanceKm").GetInt32() - expected) <= 1,
            $"Distância do cartão: {card.GetProperty("distanceKm").GetInt32()} km; distância calculada: {expected:0.000} km.");
    }

    /// <summary>
    /// O cartão tem os doze campos da API v02 (8.2): função, Empresa e logótipo, intervalo salarial, localidade, distância,
    /// tipo de contrato, regime, urgência e fotografias. O logótipo e as fotografias só aparecem quando registados; sem eles
    /// o cartão tem os restantes dados (RF013).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CardHasTheDocumentedFieldsAndTheFilesOnlyWhenRegistered()
    {
        (_, string token) = await this.SeedCandidateAsync();
        string logo = $"company_logo/{Guid.NewGuid():N}.png";
        string photo1 = $"job_photo/{Guid.NewGuid():N}.jpg";
        string photo2 = $"job_photo/{Guid.NewGuid():N}.png";
        Guid withFiles = await this.SeedJobAsync(
            12,
            WorkMode.Hybrid,
            minSalary: 1600.00m,
            maxSalary: 2100.00m,
            isUrgent: true,
            configure: job =>
            {
                job.AddPhoto(photo1);
                job.AddPhoto(photo2);
            },
            configureCompany: company => company.SetLogo(logo),
            companyName: "Lumen Software, Lda.");
        Guid withoutFiles = await this.SeedJobAsync(12, WorkMode.OnSite, contractType: ContractType.Internship);

        List<JsonElement> cards = await this.WalkAsync(token);

        JsonElement full = cards.Single(card => JobId(card) == withFiles);
        Assert.Equal(
            [
                "companyName", "contractType", "distanceKm", "isUrgent", "jobId", "locationName",
                "logoUrl", "maxSalary", "minSalary", "photoUrls", "title", "workMode",
            ],
            full.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Programador Backend .NET", full.GetProperty("title").GetString());
        Assert.Equal("Lumen Software, Lda.", full.GetProperty("companyName").GetString());
        Assert.Equal("/api/files/COMPANY_LOGO/" + logo.Split('/')[1], full.GetProperty("logoUrl").GetString());
        Assert.Equal(1600.00m, full.GetProperty("minSalary").GetDecimal());
        Assert.Equal(2100.00m, full.GetProperty("maxSalary").GetDecimal());
        Assert.StartsWith("Vaga ", full.GetProperty("locationName").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("(Braga)", full.GetProperty("locationName").GetString(), StringComparison.Ordinal);
        Assert.Equal(12, full.GetProperty("distanceKm").GetInt32());
        Assert.Equal("PERMANENT", full.GetProperty("contractType").GetString());
        Assert.Equal("HYBRID", full.GetProperty("workMode").GetString());
        Assert.True(full.GetProperty("isUrgent").GetBoolean());
        Assert.Equal(
            ["/api/files/JOB_PHOTO/" + photo1.Split('/')[1], "/api/files/JOB_PHOTO/" + photo2.Split('/')[1]],
            full.GetProperty("photoUrls").EnumerateArray().Select(url => url.GetString()));

        JsonElement bare = cards.Single(card => JobId(card) == withoutFiles);
        Assert.Equal(JsonValueKind.Null, bare.GetProperty("logoUrl").ValueKind);
        Assert.Equal(0, bare.GetProperty("photoUrls").GetArrayLength());
        Assert.Equal("INTERNSHIP", bare.GetProperty("contractType").GetString());
        Assert.Equal("ON_SITE", bare.GetProperty("workMode").GetString());
        Assert.False(bare.GetProperty("isUrgent").GetBoolean());
    }

    /// <summary>
    /// Os cartões são apresentados um de cada vez: sem resposta, o pedido seguinte devolve o mesmo cartão; depois da recusa
    /// ou do interesse, a vaga não volta a surgir. A ordem é primeiro as urgentes e depois a mais próxima.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CardsComeOneAtATimeAndAnsweredJobsNeverComeBack()
    {
        (_, string token) = await this.SeedCandidateAsync(candidate => candidate.MaxDistanceKm = 60);
        Guid far = await this.SeedJobAsync(50, WorkMode.OnSite);
        Guid near = await this.SeedJobAsync(2, WorkMode.OnSite);
        Guid urgentFar = await this.SeedJobAsync(55, WorkMode.OnSite, isUrgent: true);

        Guid first = JobId(await this.NextAsync(token));
        Guid again = JobId(await this.NextAsync(token));
        Assert.Equal(first, again);

        // Responde com interesse ao primeiro cartão e recusa os restantes, até ao fim.
        using (HttpResponseMessage answer = await this.SendAsync(HttpMethod.Post, $"{first}/interest", token))
        {
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        }

        List<Guid> sequence = [first, .. JobIds(await this.WalkAsync(token))];

        Assert.Equal(sequence.Count, sequence.Distinct().Count());
        Guid[] created = [far, near, urgentFar];
        Assert.Equal([urgentFar, near, far], sequence.Where(id => created.Contains(id)));
        Assert.Empty(await this.WalkAsync(token));
    }

    /// <summary>
    /// Sem vagas que cumpram o filtro, a resposta é 204, sem corpo (UC05, A6).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NoEligibleJobReturns204()
    {
        (_, string token) = await this.SeedCandidateAsync(candidate => candidate.MinSalaryExpectation = 99_999_999.99m);
        await this.SeedJobAsync(5, WorkMode.OnSite);

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "next", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Com a quota de interesses esgotada, os cartões continuam a ser apresentados (UC05, A4).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CardsAreShownWithTheQuotaExhausted()
    {
        (_, string token) = await this.SeedCandidateAsync(interests: Candidate.MaxInterests);
        Guid job = await this.SeedJobAsync(5, WorkMode.OnSite);

        List<JsonElement> cards = await this.WalkAsync(token);

        Assert.Contains(job, JobIds(cards));
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
    public async Task OnlyTheCandidateGetsCards(UserType? userType)
    {
        string? token = null;
        if (userType is { } type)
        {
            AppUser user = await AuthTestAccounts.SeedAsync(this.factory, type);
            token = this.IssueToken(user);
        }

        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "next", token);

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

    /// <summary>
    /// O pedido do cartão responde em menos de 2 segundos (RNF002, P19), também depois de várias vagas respondidas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NextCardRespondsWithinTwoSeconds()
    {
        (_, string token) = await this.SeedCandidateAsync(candidate => candidate.MaxDistanceKm = 100);
        for (int index = 0; index < 20; index++)
        {
            await this.SeedJobAsync(index * 4, index % 3 == 0 ? WorkMode.Remote : WorkMode.OnSite);
        }

        // Primeiro pedido fora da medição: aquece a aplicação e o modelo do EF Core.
        (await this.SendAsync(HttpMethod.Get, "next", token)).Dispose();

        var watch = Stopwatch.StartNew();
        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "next", token);
        watch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"O pedido demorou {watch.Elapsed.TotalMilliseconds:0} ms.");
    }

    private static double Haversine(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;

        double halfLatitude = Math.Sin((Radians(latitude2) - Radians(latitude1)) / 2);
        double halfLongitude = Math.Sin((Radians(longitude2) - Radians(longitude1)) / 2);
        double a = (halfLatitude * halfLatitude)
            + (Math.Cos(Radians(latitude1)) * Math.Cos(Radians(latitude2)) * halfLongitude * halfLongitude);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }

    private static async Task<JsonElement> ReadCardAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(body);
        return json.RootElement.Clone();
    }

    private static Guid JobId(JsonElement card)
    {
        return card.GetProperty("jobId").GetGuid();
    }

    private static IEnumerable<Guid> JobIds(IEnumerable<JsonElement> cards)
    {
        return cards.Select(JobId);
    }

    private static CompanyRegistration Registration(Guid locationId, string companyName)
    {
        return new CompanyRegistration
        {
            CompanyName = companyName,
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
    private static Location LocationNorthOfBraga(int kilometres)
    {
        decimal latitude = Math.Round(BragaLatitude + (decimal)(kilometres / (EarthRadiusKm * Math.PI / 180)), 6);
        return new Location($"Vaga {Guid.NewGuid():N}", "Braga", latitude, BragaLongitude);
    }

    private async Task<(Candidate Candidate, string Token)> SeedCandidateAsync(
        Action<Candidate>? configure = null,
        int interests = 0)
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Candidate, created, created);
        var candidate = new Candidate(user, "Ana Silva", "912345678", Guid.NewGuid());
        for (int index = 0; index < interests; index++)
        {
            candidate.ConsumeInterest(created);
        }

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

    private async Task<Guid> SeedJobAsync(
        int kilometresNorth,
        WorkMode workMode,
        decimal minSalary = 1000m,
        decimal maxSalary = 2000m,
        ContractType contractType = ContractType.Permanent,
        bool isUrgent = false,
        CompanyStatus company = CompanyStatus.Approved,
        DateTimeOffset? expiresAt = null,
        bool publish = true,
        Action<Job>? configure = null,
        Action<Company>? configureCompany = null,
        string companyName = "Empresa Exemplo, Lda.")
    {
        Location location = LocationNorthOfBraga(kilometresNorth);
        await this.SaveAsync(context => context.Locations.Add(location));
        return await this.SeedJobAsync(
            location,
            workMode,
            minSalary,
            maxSalary,
            contractType,
            isUrgent,
            company,
            expiresAt,
            publish,
            configure,
            configureCompany,
            companyName);
    }

    private async Task<Guid> SeedJobAsync(
        Location location,
        WorkMode workMode,
        decimal minSalary = 1000m,
        decimal maxSalary = 2000m,
        ContractType contractType = ContractType.Permanent,
        bool isUrgent = false,
        CompanyStatus company = CompanyStatus.Approved,
        DateTimeOffset? expiresAt = null,
        bool publish = true,
        Action<Job>? configure = null,
        Action<Company>? configureCompany = null,
        string companyName = "Empresa Exemplo, Lda.")
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Recruiter, created, created);
        var recruiter = new Recruiter(user);
        var companyEntity = new Company(recruiter.Id, Registration(location.Id, companyName), created);
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
                Description = "Descrição da vaga.",
                MinSalary = minSalary,
                MaxSalary = maxSalary,
                LocationId = location.Id,
                ContractType = contractType,
                WorkMode = workMode,
                IsUrgent = isUrgent,
                ExpiresAt = expiresAt,
            });
        if (publish)
        {
            job.Publish(this.now.AddDays(-10));
        }

        configure?.Invoke(job);

        await this.SaveAsync(context =>
        {
            context.Recruiters.Add(recruiter);
            context.Companies.Add(companyEntity);
            context.Jobs.Add(job);
        });
        return job.Id;
    }

    /// <summary>
    /// Pede o cartão seguinte e recusa-o, até a resposta ser 204; devolve os cartões pela ordem em que foram apresentados.
    /// </summary>
    /// <param name="token">Credencial do Candidato.</param>
    /// <returns>Os cartões apresentados.</returns>
    private async Task<List<JsonElement>> WalkAsync(string token)
    {
        List<JsonElement> cards = [];
        for (int step = 0; step < 1000; step++)
        {
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "next", token);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return cards;
            }

            JsonElement card = await ReadCardAsync(response);
            cards.Add(card);

            using HttpResponseMessage decline = await this.SendAsync(HttpMethod.Post, $"{JobId(card)}/decline", token);
            Assert.Equal(HttpStatusCode.NoContent, decline.StatusCode);
        }

        throw new InvalidOperationException("Os cartões não terminaram em 1000 pedidos.");
    }

    private async Task<JsonElement> NextAsync(string token)
    {
        using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "next", token);
        return await ReadCardAsync(response);
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
