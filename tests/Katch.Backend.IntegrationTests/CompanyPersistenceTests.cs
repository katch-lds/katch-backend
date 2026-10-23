// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de persistência de <see cref="Company"/> sobre SQLite in-memory: gravação e leitura, unicidade do NIF e do
/// Recrutador e chaves estrangeiras. Os <c>CHECK</c>, os tipos enumerados e o array <c>varchar(500)[]</c> são específicos
/// do PostgreSQL e estão verificados nos testes unitários do mapeamento.
/// </summary>
public sealed class CompanyPersistenceTests : IClassFixture<KatchWebApplicationFactory>
{
    private static readonly DateTimeOffset Submitted = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyPersistenceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public CompanyPersistenceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// A Empresa aprovada é gravada com os dados de registo, a página e a galeria, e lida de volta igual, com as
    /// fotografias pela ordem em que foram acrescentadas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CompanyIsSavedAndReadBackWithPageAndGalleryInOrder()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (Recruiter recruiter, Location location) = await this.SaveRecruiterAndLocationAsync("lumen@katch.pt");
        var company = new Company(recruiter.Id, Registration(location.Id, "509123456"), Submitted);
        company.Approve();
        company.UpdatePage("Software de gestão de frotas.", "https://www.lumen-exemplo.pt");
        company.SetLogo("company-logos/lumen.png");
        company.AddGalleryPhoto("company-photos/b.jpg");
        company.AddGalleryPhoto("company-photos/a.jpg");

        await this.SaveAsync(company);

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        Company saved = await context.Companies.SingleAsync(item => item.Id == company.Id, cancellationToken);

        Assert.Equal(recruiter.Id, saved.RecruiterId);
        Assert.Equal("Lumen Software, Lda.", saved.CompanyName);
        Assert.Equal("509123456", saved.TaxId);
        Assert.Equal(Industry.Technology, saved.Industry);
        Assert.Equal("Avenida da Liberdade, 100, 4710-000 Braga", saved.Address);
        Assert.Equal(location.Id, saved.LocationId);
        Assert.Equal("geral@lumen-exemplo.pt", saved.ContactEmail);
        Assert.Equal("253000111", saved.ContactPhone);
        Assert.Equal("Rui Matos", saved.ResponsibleName);
        Assert.Equal("Software de gestão de frotas.", saved.Description);
        Assert.Equal("https://www.lumen-exemplo.pt", saved.Website);
        Assert.Equal("company-logos/lumen.png", saved.LogoPath);
        Assert.Equal(["company-photos/b.jpg", "company-photos/a.jpg"], saved.PhotoPaths);
        Assert.Equal(CompanyStatus.Approved, saved.Status);
        Assert.Null(saved.StatusReason);
        Assert.Equal(Submitted, saved.SubmittedAt);
    }

    /// <summary>
    /// A recusa e a nova submissão são gravadas: o motivo fica guardado e é limpo na nova submissão.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectionAndResubmissionArePersisted()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (Recruiter recruiter, Location location) = await this.SaveRecruiterAndLocationAsync("recusa@katch.pt");
        var company = new Company(recruiter.Id, Registration(location.Id, "509000001"), Submitted);
        company.Reject("A morada não corresponde à certidão.");
        await this.SaveAsync(company);

        Assert.Equal("A morada não corresponde à certidão.", (await this.ReadAsync(company.Id)).StatusReason);

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            Company stored = await context.Companies.SingleAsync(item => item.Id == company.Id, cancellationToken);
            stored.Resubmit(Registration(location.Id, "509000002"), Submitted.AddDays(1));
            await context.SaveChangesAsync(cancellationToken);
        }

        Company resubmitted = await this.ReadAsync(company.Id);
        Assert.Equal(CompanyStatus.Pending, resubmitted.Status);
        Assert.Null(resubmitted.StatusReason);
        Assert.Equal("509000002", resubmitted.TaxId);
        Assert.Equal(Submitted.AddDays(1), resubmitted.SubmittedAt);
    }

    /// <summary>
    /// Dois registos com o mesmo NIF são rejeitados pelo índice único <c>uq_company_tax_id</c> (RF042).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DuplicateTaxIdIsRejected()
    {
        (Recruiter first, Location location) = await this.SaveRecruiterAndLocationAsync("nif-1@katch.pt");
        (Recruiter second, _) = await this.SaveRecruiterAndLocationAsync("nif-2@katch.pt");
        await this.SaveAsync(new Company(first.Id, Registration(location.Id, "509111111"), Submitted));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => this.SaveAsync(new Company(second.Id, Registration(location.Id, "509111111"), Submitted)));
    }

    /// <summary>
    /// Um Recrutador representa no máximo uma Empresa: o segundo registo é rejeitado por <c>uq_company_recruiter</c>.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SecondCompanyForTheSameRecruiterIsRejected()
    {
        (Recruiter recruiter, Location location) = await this.SaveRecruiterAndLocationAsync("duas@katch.pt");
        await this.SaveAsync(new Company(recruiter.Id, Registration(location.Id, "509222221"), Submitted));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => this.SaveAsync(new Company(recruiter.Id, Registration(location.Id, "509222222"), Submitted)));
    }

    /// <summary>
    /// Uma Empresa com um Recrutador ou uma localidade inexistentes é rejeitada pelas chaves estrangeiras.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownRecruiterOrLocationIsRejectedByTheForeignKeys()
    {
        (Recruiter recruiter, Location location) = await this.SaveRecruiterAndLocationAsync("fk@katch.pt");

        await Assert.ThrowsAsync<DbUpdateException>(
            () => this.SaveAsync(new Company(Guid.NewGuid(), Registration(location.Id, "509333331"), Submitted)));
        await Assert.ThrowsAsync<DbUpdateException>(
            () => this.SaveAsync(new Company(recruiter.Id, Registration(Guid.NewGuid(), "509333332"), Submitted)));
    }

    private static CompanyRegistration Registration(Guid locationId, string taxId)
    {
        return new CompanyRegistration
        {
            CompanyName = "Lumen Software, Lda.",
            TaxId = taxId,
            Industry = Industry.Technology,
            Address = "Avenida da Liberdade, 100, 4710-000 Braga",
            LocationId = locationId,
            ContactEmail = "geral@lumen-exemplo.pt",
            ContactPhone = "253000111",
            ResponsibleName = "Rui Matos",
        };
    }

    private async Task<(Recruiter Recruiter, Location Location)> SaveRecruiterAndLocationAsync(string email)
    {
        var user = new AppUser(email, "hash", UserType.Recruiter, Submitted, Submitted);
        var recruiter = new Recruiter(user);
        var location = new Location("Braga " + email, "Braga", 41.545450m, -8.426507m);

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        context.Recruiters.Add(recruiter);
        context.Locations.Add(location);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (recruiter, location);
    }

    private async Task SaveAsync(Company company)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        context.Companies.Add(company);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Company> ReadAsync(Guid companyId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.Companies
            .AsNoTracking()
            .SingleAsync(item => item.Id == companyId, TestContext.Current.CancellationToken);
    }
}
