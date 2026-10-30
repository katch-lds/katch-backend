// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;
using Katch.Backend.Services;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes do <see cref="MatchService"/> (documentação da API v02, secção 6.7.1), com repositórios em memória.
/// </summary>
public sealed class MatchQueryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Para o Candidato, a outra parte é o responsável da Empresa e os contactos são os de registo da Empresa (RF025).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CandidateSeesTheCompanyResponsibleAndTheCompanyContacts()
    {
        MatchView view = NewView(Now.AddDays(-1), logoPath: "company_logo/lumen.png");
        var (service, matches, _) = Create(view);

        List<MatchDto> result = await service.ListForCandidateAsync(view.Match.CandidateId, TestContext.Current.CancellationToken);

        MatchDto dto = Assert.Single(result);
        Assert.Equal(view.Match.Id, dto.MatchId);
        Assert.Equal("Programador Backend .NET", dto.JobTitle);
        Assert.Equal(JobStatus.Published, dto.JobStatus);
        Assert.Equal("Braga (Braga)", dto.LocationName);
        Assert.Equal("Lumen Software, Lda.", dto.CompanyName);
        Assert.Equal("/files/company_logo/lumen.png", dto.LogoUrl);
        Assert.Equal("Rui Matos", dto.CounterpartName);
        Assert.Equal("recrutamento@lumen-exemplo.pt", dto.ContactEmail);
        Assert.Equal("253000222", dto.ContactPhone);
        Assert.Equal(view.Match.RecruiterActionAt, dto.MatchedAt);
        Assert.Equal(view.Match.CandidateId, matches.QueriedCandidate);
    }

    /// <summary>
    /// Para o Recrutador, a outra parte é o Candidato e os contactos são os do Candidato (RF067); sem logótipo o URL é nulo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RecruiterSeesTheCandidateAndTheCandidateContacts()
    {
        MatchView view = NewView(Now.AddDays(-1));
        var (service, matches, companies) = Create(view);
        Company company = NewCompany();
        companies.Company = company;

        List<MatchDto> result = await service.ListForRecruiterAsync(company.RecruiterId, TestContext.Current.CancellationToken);

        MatchDto dto = Assert.Single(result);
        Assert.Equal("Ana Ferreira", dto.CounterpartName);
        Assert.Equal("ana@exemplo.pt", dto.ContactEmail);
        Assert.Equal("912345678", dto.ContactPhone);
        Assert.Null(dto.LogoUrl);
        Assert.Equal(company.Id, matches.QueriedCompany);
    }

    /// <summary>
    /// A lista sai do match mais recente para o mais antigo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListsFromTheNewestToTheOldest()
    {
        MatchView oldest = NewView(Now.AddDays(-5));
        MatchView newest = NewView(Now.AddDays(-1));
        MatchView middle = NewView(Now.AddDays(-3));
        var (service, _, _) = Create(oldest, newest, middle);

        List<MatchDto> result = await service.ListForCandidateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal([newest.Match.Id, middle.Match.Id, oldest.Match.Id], result.Select(dto => dto.MatchId).ToList());
    }

    /// <summary>
    /// Sem matches, ou sem Empresa registada, a lista é vazia.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReturnsAnEmptyListWithoutMatchesOrWithoutACompany()
    {
        var (service, _, _) = Create();

        Assert.Empty(await service.ListForCandidateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Empty(await service.ListForRecruiterAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    private static (MatchService Service, FakeMatches Matches, FakeCompanies Companies) Create(params MatchView[] views)
    {
        var matches = new FakeMatches(views);
        var companies = new FakeCompanies();
        return (new MatchService(matches, companies, new FakeFileUrls()), matches, companies);
    }

    private static Company NewCompany()
    {
        return new Company(
            Guid.NewGuid(),
            new CompanyRegistration
            {
                CompanyName = "Lumen Software, Lda.",
                TaxId = "500000001",
                Industry = Industry.Technology,
                Address = "Rua de Exemplo, 1, 4700-000 Braga",
                LocationId = Guid.NewGuid(),
                ContactEmail = "recrutamento@lumen-exemplo.pt",
                ContactPhone = "253000222",
                ResponsibleName = "Rui Matos",
            },
            Now);
    }

    private static MatchView NewView(DateTimeOffset matchedAt, string? logoPath = null)
    {
        var match = Match.ExpressInterest(Guid.NewGuid(), Guid.NewGuid(), matchedAt.AddHours(-2));
        match.OpenProfile(matchedAt.AddHours(-1));
        match.Accept(Guid.NewGuid(), matchedAt);
        return new MatchView(
            match,
            "Programador Backend .NET",
            JobStatus.Published,
            new Location("Braga", "Braga", 41.545450m, -8.426507m),
            "Lumen Software, Lda.",
            logoPath,
            "Rui Matos",
            "recrutamento@lumen-exemplo.pt",
            "253000222",
            "Ana Ferreira",
            "ana@exemplo.pt",
            "912345678");
    }

    private sealed class FakeFileUrls : IFileUrlBuilder
    {
        public string? ToUrl(string? path)
        {
            return path is null ? null : string.Create(CultureInfo.InvariantCulture, $"/files/{path}");
        }

        public List<string> ToUrls(List<string> paths)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeCompanies : ICompanyRepository
    {
        public Company? Company { get; set; }

        public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Company);
        }

        public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeMatches : IMatchRepository
    {
        private readonly List<MatchView> views;

        public FakeMatches(IEnumerable<MatchView> views)
        {
            this.views = views.ToList();
        }

        public Guid? QueriedCandidate { get; private set; }

        public Guid? QueriedCompany { get; private set; }

        public Task<List<MatchView>> ListMatchViewsByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
        {
            this.QueriedCandidate = this.views.Count > 0 ? this.views[0].Match.CandidateId : candidateId;
            return Task.FromResult(this.views.ToList());
        }

        public Task<List<MatchView>> ListMatchViewsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            this.QueriedCompany = companyId;
            return Task.FromResult(this.views.ToList());
        }

        public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Match match, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> HasInterestFromCandidateInCompanyAsync(Guid candidateId, Guid recruiterId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Guid?> GetRecruiterIdAsync(Guid matchId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<ConversationView>> ListConversationsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
