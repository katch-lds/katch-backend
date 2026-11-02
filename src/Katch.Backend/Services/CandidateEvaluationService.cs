// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="ICandidateEvaluationService"/> (modelo de classes do backend v02, secções 5.1 e 5.3).
/// A ordem das verificações é a mesma nas três operações: elemento inexistente (ERR-20), vaga de outra Empresa
/// (ERR-08) e interesse que já não está em espera (ERR-35). A Empresa aprovada é garantida antes, pela política
/// <c>ApprovedCompany</c> (ERR-07).
/// </summary>
public sealed class CandidateEvaluationService : ICandidateEvaluationService
{
    /// <summary>
    /// Nome do ficheiro do curriculum vitae enviado ao Recrutador, que não revela o caminho no servidor (6.6.3).
    /// </summary>
    internal const string CvFileName = "cv.pdf";

    /// <summary>
    /// Número máximo de tentativas da abertura do perfil perante conflitos de concorrência (<c>xmin</c>), por exemplo
    /// duas aberturas simultâneas do mesmo perfil em dois separadores.
    /// </summary>
    internal const int MaxOpenProfileAttempts = 3;

    private static readonly StringComparer Alphabetical = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true);

    private readonly IMatchRepository matches;
    private readonly IJobRepository jobs;
    private readonly ICompanyRepository companies;
    private readonly ICandidateRepository candidates;
    private readonly IReferenceDataRepository referenceData;
    private readonly IUnitOfWork unitOfWork;
    private readonly IFileStorage fileStorage;
    private readonly IFileUrlBuilder fileUrlBuilder;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateEvaluationService"/> class.
    /// </summary>
    /// <param name="matches">Repositório das respostas dos Candidatos às vagas.</param>
    /// <param name="jobs">Repositório das vagas.</param>
    /// <param name="companies">Repositório das Empresas.</param>
    /// <param name="candidates">Repositório dos Candidatos.</param>
    /// <param name="referenceData">Repositório das listas pré-definidas (localidades).</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    /// <param name="fileStorage">Armazenamento dos ficheiros.</param>
    /// <param name="fileUrlBuilder">Conversão dos caminhos guardados nos URLs da API.</param>
    /// <param name="clock">Relógio do servidor, a única fonte do instante da abertura do perfil.</param>
    public CandidateEvaluationService(
        IMatchRepository matches,
        IJobRepository jobs,
        ICompanyRepository companies,
        ICandidateRepository candidates,
        IReferenceDataRepository referenceData,
        IUnitOfWork unitOfWork,
        IFileStorage fileStorage,
        IFileUrlBuilder fileUrlBuilder,
        IClock clock)
    {
        this.matches = matches;
        this.jobs = jobs;
        this.companies = companies;
        this.candidates = candidates;
        this.referenceData = referenceData;
        this.unitOfWork = unitOfWork;
        this.fileStorage = fileStorage;
        this.fileUrlBuilder = fileUrlBuilder;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public async Task<List<WaitingCandidateDto>> ListWaitingAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default)
    {
        Job job = await this.jobs.GetWithSkillsAsync(jobId, cancellationToken)
            ?? throw new ElementNotFoundException();
        await this.EnsureOwnJobAsync(recruiterId, job, cancellationToken);

        List<WaitingInterest> waiting = await this.matches.ListWaitingByJobAsync(jobId, cancellationToken);
        return waiting
            .Select(interest => new WaitingCandidateDto(
                interest.MatchId,
                interest.CandidateId,
                interest.FullName,
                interest.DesiredRole,
                ReferenceListService.DisplayName(interest.Location),
                interest.InterestAt))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<CandidateFullProfileDto> OpenProfileAsync(Guid recruiterId, Guid matchId, CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await this.TryOpenProfileAsync(recruiterId, matchId, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxOpenProfileAttempts)
            {
                // Outro pedido alterou o interesse entretanto (outra abertura ou uma decisão): repete-se tudo desde a
                // leitura, para devolver ERR-35 se a decisão já foi registada ou o perfil se continua em espera.
                this.unitOfWork.DiscardChanges();
            }
        }
    }

    /// <inheritdoc/>
    public async Task<FileContent> GetCvAsync(Guid recruiterId, Guid matchId, CancellationToken cancellationToken = default)
    {
        (Match match, _) = await this.GetWaitingInterestAsync(recruiterId, matchId, cancellationToken);
        Candidate candidate = await this.candidates.GetByIdAsync(match.CandidateId, cancellationToken)
            ?? throw new ElementNotFoundException();
        string path = candidate.CvPath ?? throw new ElementNotFoundException();

        try
        {
            Stream content = await this.fileStorage.OpenReadAsync(path, cancellationToken);
            return new FileContent(content, FileRules.ContentType(FileFormat.Pdf), CvFileName);
        }
        catch (FileNotFoundException)
        {
            throw new ElementNotFoundException();
        }
    }

    private static List<SkillTagDto> ToSkillTags(Candidate candidate, Job job)
    {
        HashSet<Guid> jobSkills = job.Skills.Select(skill => skill.Id).ToHashSet();

        // Só as competências da lista podem coincidir com as da vaga; as da opção «Outro» nunca são destacadas (RF062).
        IEnumerable<SkillTagDto> listed = candidate.Skills
            .Select(skill => new SkillTagDto(skill.Name, jobSkills.Contains(skill.Id)));
        IEnumerable<SkillTagDto> custom = candidate.CustomSkills
            .Select(customSkill => new SkillTagDto(customSkill.Label, false));

        return listed.Concat(custom).OrderBy(tag => tag.Name, Alphabetical).ToList();
    }

    private async Task<CandidateFullProfileDto> TryOpenProfileAsync(Guid recruiterId, Guid matchId, CancellationToken cancellationToken)
    {
        (Match match, Job job) = await this.GetWaitingInterestAsync(recruiterId, matchId, cancellationToken);
        Candidate candidate = await this.candidates.GetWithProfileAsync(match.CandidateId, cancellationToken)
            ?? throw new ElementNotFoundException();
        Location location = await this.referenceData.GetLocationAsync(candidate.LocationId, cancellationToken)
            ?? throw new InvalidOperationException("A localidade do Candidato não existe na lista pré-definida.");

        // Só a primeira abertura é gravada (RF118); as seguintes não alteram nada e não escrevem na base de dados.
        if (match.OpenProfile(this.clock.UtcNow))
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new CandidateFullProfileDto(
            match.Id,
            candidate.FullName,
            this.fileUrlBuilder.ToUrl(candidate.ProfilePhotoPath),
            candidate.DesiredRole,
            ReferenceListService.DisplayName(location),
            candidate.Availability,
            candidate.ExperienceSummary,
            candidate.Links.Select(link => link.Url).ToList(),
            ToSkillTags(candidate, job),
            candidate.CvPath is not null,
            match.ProfileOpenedAt!.Value);
    }

    /// <summary>
    /// Obtém o interesse e a sua vaga e confirma, por esta ordem, que o interesse existe e é um interesse (ERR-20), que
    /// a vaga é da Empresa do Recrutador (ERR-08) e que o interesse continua em espera (ERR-35).
    /// </summary>
    private async Task<(Match Match, Job Job)> GetWaitingInterestAsync(Guid recruiterId, Guid matchId, CancellationToken cancellationToken)
    {
        Match match = await this.matches.GetByIdAsync(matchId, cancellationToken)
            ?? throw new ElementNotFoundException();

        // A recusa da vaga pelo Candidato não é um interesse: não há perfil a abrir (UC11, E3).
        if (!match.IsInterest())
        {
            throw new ElementNotFoundException();
        }

        Job job = await this.jobs.GetWithSkillsAsync(match.JobId, cancellationToken)
            ?? throw new ElementNotFoundException();
        await this.EnsureOwnJobAsync(recruiterId, job, cancellationToken);

        if (match.Status != MatchStatus.Waiting)
        {
            throw new OperationConflictException(ApiErrorResponse.CandidateAlreadyEvaluated());
        }

        return (match, job);
    }

    private async Task EnsureOwnJobAsync(Guid recruiterId, Job job, CancellationToken cancellationToken)
    {
        Company? company = await this.companies.GetByRecruiterAsync(recruiterId, cancellationToken);
        if (company is null || company.Id != job.CompanyId)
        {
            throw new ElementForbiddenException();
        }
    }
}
