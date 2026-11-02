// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IMatchService"/> (modelo de classes do backend v02, secção 5).
/// </summary>
public sealed class MatchService : IMatchService
{
    private readonly IMatchRepository matches;
    private readonly ICompanyRepository companies;
    private readonly IFileUrlBuilder fileUrls;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchService"/> class.
    /// </summary>
    /// <param name="matches">Repositório dos matches.</param>
    /// <param name="companies">Repositório das Empresas.</param>
    /// <param name="fileUrls">Construtor dos URLs dos ficheiros.</param>
    public MatchService(IMatchRepository matches, ICompanyRepository companies, IFileUrlBuilder fileUrls)
    {
        this.matches = matches;
        this.companies = companies;
        this.fileUrls = fileUrls;
    }

    /// <inheritdoc/>
    public async Task<List<MatchDto>> ListForCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        List<MatchView> views = await this.matches.ListMatchViewsByCandidateAsync(candidateId, cancellationToken);

        return this.ToDtos(views, view => (view.ResponsibleName, view.CompanyContactEmail, view.CompanyContactPhone));
    }

    /// <inheritdoc/>
    public async Task<List<MatchDto>> ListForRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
    {
        Company? company = await this.companies.GetByRecruiterAsync(recruiterId, cancellationToken);
        if (company is null)
        {
            return [];
        }

        List<MatchView> views = await this.matches.ListMatchViewsByCompanyAsync(company.Id, cancellationToken);

        return this.ToDtos(views, view => (view.CandidateName, view.CandidateEmail, view.CandidatePhone));
    }

    private List<MatchDto> ToDtos(List<MatchView> views, Func<MatchView, (string Name, string Email, string Phone)> counterpart)
    {
        return views
            .Select(view => (View: view, MatchedAt: view.Match.RecruiterActionAt ?? view.Match.CandidateActionAt))
            .OrderByDescending(item => item.MatchedAt)
            .Select(item =>
            {
                (string name, string email, string phone) = counterpart(item.View);
                return new MatchDto(
                    item.View.Match.Id,
                    item.View.JobTitle,
                    item.View.JobStatus,
                    ReferenceListService.DisplayName(item.View.Location),
                    item.View.CompanyName,
                    this.fileUrls.ToUrl(item.View.CompanyLogoPath),
                    name,
                    email,
                    phone,
                    item.MatchedAt);
            })
            .ToList();
    }
}
