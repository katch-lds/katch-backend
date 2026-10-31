// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Net.Http.Headers;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Dados e pedidos partilhados pelos testes do perfil do Candidato e da obtenção de ficheiros. Os dados são fictícios e
/// os ficheiros são mínimos, só com a assinatura do formato.
/// </summary>
internal static class CandidateProfileTestData
{
    /// <summary>
    /// Número de bytes de um megabyte.
    /// </summary>
    internal const int Megabyte = 1024 * 1024;

    private static long taxIdCounter = 700_000_000;

    /// <summary>
    /// Gets a minimal PNG file (signature only).
    /// </summary>
    internal static byte[] Png { get; } = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    /// <summary>
    /// Gets a minimal JPEG file (signature only).
    /// </summary>
    internal static byte[] Jpeg { get; } = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    /// <summary>
    /// Gets a minimal PDF file (signature only).
    /// </summary>
    internal static byte[] Pdf { get; } = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];

    /// <summary>
    /// Cria o conteúdo de um ficheiro com a assinatura indicada e a dimensão pedida.
    /// </summary>
    /// <param name="signature">Primeiros bytes do ficheiro.</param>
    /// <param name="length">Dimensão total, em bytes.</param>
    /// <returns>O conteúdo.</returns>
    internal static byte[] WithLength(byte[] signature, int length)
    {
        byte[] content = new byte[length];
        signature.CopyTo(content, 0);
        return content;
    }

    /// <summary>
    /// Grava um Candidato novo, com a sua localidade, e emite a credencial de sessão.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="configure">Alteração opcional do Candidato antes de gravar.</param>
    /// <returns>O Candidato, a sua localidade e a credencial.</returns>
    internal static async Task<(Candidate Candidate, Location Location, string Token)> SeedCandidateAsync(
        KatchWebApplicationFactory factory,
        Action<Candidate>? configure = null)
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Candidate, created, created);
        var location = new Location($"Guimarães {Guid.NewGuid():N}", "Braga", 41.445m, -8.292m);
        var candidate = new Candidate(user, "Ana Ferreira", "912345678", location.Id);
        configure?.Invoke(candidate);

        await SaveAsync(factory, context =>
        {
            context.Locations.Add(location);
            context.Candidates.Add(candidate);
        });
        return (candidate, location, IssueToken(factory, user));
    }

    /// <summary>
    /// Acrescenta uma localidade à lista pré-definida.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <returns>A localidade gravada.</returns>
    internal static async Task<Location> SeedLocationAsync(KatchWebApplicationFactory factory)
    {
        var location = new Location($"Porto {Guid.NewGuid():N}", "Porto", 41.15m, -8.61m);
        await SaveAsync(factory, context => context.Locations.Add(location));
        return location;
    }

    /// <summary>
    /// Grava uma conta de Recrutador ou de Administrador e emite a credencial.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="userType">Tipo de conta.</param>
    /// <returns>A conta e a credencial.</returns>
    internal static async Task<(AppUser User, string Token)> SeedAccountAsync(KatchWebApplicationFactory factory, UserType userType)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(factory, userType);
        return (user, IssueToken(factory, user));
    }

    /// <summary>
    /// Grava uma Empresa aprovada do Recrutador, com uma vaga publicada, e a resposta do Candidato a essa vaga.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="candidateId">Candidato que responde à vaga.</param>
    /// <param name="status">Estado da resposta: recusa, em espera ou match.</param>
    /// <returns>A credencial do Recrutador, dono da Empresa da vaga.</returns>
    internal static async Task<string> SeedInterestAsync(KatchWebApplicationFactory factory, Guid candidateId, MatchStatus status)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", UserType.Recruiter, now, now);
        var recruiter = new Recruiter(user);
        var location = new Location($"Braga {Guid.NewGuid():N}", "Braga", 41.545450m, -8.426507m);
        var company = new Company(recruiter.Id, Registration(location.Id), now);
        company.Approve();
        var job = new Job(
            company.Id,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição da vaga.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        job.Publish(now.AddDays(-10));

        Match match = status == MatchStatus.DeclinedByCandidate
            ? Match.Decline(candidateId, job.Id, now)
            : Match.ExpressInterest(candidateId, job.Id, now);
        if (status == MatchStatus.Matched)
        {
            match.OpenProfile(now);
            match.Accept(recruiter.Id, now);
        }

        await SaveAsync(factory, context =>
        {
            context.Recruiters.Add(recruiter);
            context.Locations.Add(location);
            context.Companies.Add(company);
            context.Jobs.Add(job);
            context.Matches.Add(match);
        });
        return IssueToken(factory, user);
    }

    /// <summary>
    /// Lê o Candidato da base de dados, sem o acompanhar.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <returns>O Candidato, com as hiperligações.</returns>
    internal static async Task<Candidate> ReadCandidateAsync(KatchWebApplicationFactory factory, Guid candidateId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Candidates.AsNoTracking()
            .Include(candidate => candidate.Links)
            .SingleAsync(candidate => candidate.Id == candidateId, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Grava um ficheiro diretamente na pasta de armazenamento, como se tivesse sido enviado.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="folder">Pasta do tipo, por exemplo <c>company_logo</c>.</param>
    /// <param name="extension">Extensão com o ponto.</param>
    /// <param name="content">Conteúdo do ficheiro.</param>
    /// <returns>O nome do ficheiro gerado.</returns>
    internal static async Task<string> StoreFileAsync(KatchWebApplicationFactory factory, string folder, string extension, byte[] content)
    {
        string name = Guid.NewGuid().ToString("N") + extension;
        string directory = Path.Combine(factory.FileStorageRoot, folder);
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, name), content, TestContext.Current.CancellationToken);
        return name;
    }

    /// <summary>
    /// Envia um pedido ao serviço.
    /// </summary>
    /// <param name="factory">Aplicação em memória.</param>
    /// <param name="method">Método HTTP.</param>
    /// <param name="path">Rota absoluta.</param>
    /// <param name="token">Credencial, ou <see langword="null"/> para a omitir.</param>
    /// <param name="content">Corpo do pedido, se houver.</param>
    /// <returns>A resposta, que quem chama liberta.</returns>
    internal static async Task<HttpResponseMessage> SendAsync(
        KatchWebApplicationFactory factory,
        HttpMethod method,
        string path,
        string? token,
        HttpContent? content = null)
    {
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative)) { Content = content };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Cria um corpo <c>multipart/form-data</c> com um campo de ficheiro.
    /// </summary>
    /// <param name="fileName">Nome do ficheiro enviado.</param>
    /// <param name="content">Conteúdo do ficheiro.</param>
    /// <param name="fieldName">Nome do campo.</param>
    /// <returns>O corpo do pedido.</returns>
    internal static MultipartFormDataContent Multipart(string fileName, byte[] content, string fieldName = "file")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(content), fieldName, fileName);
        return form;
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

    private static CompanyRegistration Registration(Guid locationId)
    {
        return new CompanyRegistration
        {
            CompanyName = "Empresa Exemplo, Lda.",
            TaxId = Interlocked.Increment(ref taxIdCounter).ToString(CultureInfo.InvariantCulture),
            Industry = Industry.Technology,
            Address = "Rua de Exemplo, 1, 4700-000 Braga",
            LocationId = locationId,
            ContactEmail = "geral@empresa-exemplo.pt",
            ContactPhone = "253000000",
            ResponsibleName = "Responsável Exemplo",
        };
    }
}
