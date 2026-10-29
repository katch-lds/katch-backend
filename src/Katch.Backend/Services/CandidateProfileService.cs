// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="ICandidateProfileService"/> (modelo de classes do backend v02, secções 5.1 e 5.3). As
/// regras que dependem só do perfil são do <see cref="Candidate"/>; este serviço valida o pedido, indicando todos os
/// campos em erro, trata os ficheiros e grava.
/// </summary>
public sealed class CandidateProfileService : ICandidateProfileService
{
    private const int DesiredRoleMaxLength = 150;
    private const int ExperienceSummaryMaxLength = 1000;
    private const int LinkMaxLength = 500;
    private const decimal SalaryMaxValue = 99_999_999.99m;
    private const string LinkRepeated = "O endereço está repetido.";
    private const string CvFileName = "curriculum-vitae.pdf";

    // Índices que impedem hiperligações repetidas quando dois pedidos do mesmo Candidato chegam ao mesmo tempo.
    private const string LinkPositionUniqueIndex = "uq_candidate_link_position";
    private const string LinkUrlUniqueIndex = "uq_candidate_link_url";

    private static readonly StringComparer Alphabetical = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true);

    private readonly ICandidateRepository candidates;
    private readonly IReferenceDataRepository referenceData;
    private readonly IUnitOfWork unitOfWork;
    private readonly IFileStorage fileStorage;
    private readonly IFileValidator fileValidator;
    private readonly IFileUrlBuilder fileUrlBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateProfileService"/> class.
    /// </summary>
    /// <param name="candidates">Repositório dos Candidatos.</param>
    /// <param name="referenceData">Repositório das listas pré-definidas.</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    /// <param name="fileStorage">Armazenamento dos ficheiros.</param>
    /// <param name="fileValidator">Validação dos ficheiros enviados.</param>
    /// <param name="fileUrlBuilder">Construtor dos URLs dos ficheiros.</param>
    public CandidateProfileService(
        ICandidateRepository candidates,
        IReferenceDataRepository referenceData,
        IUnitOfWork unitOfWork,
        IFileStorage fileStorage,
        IFileValidator fileValidator,
        IFileUrlBuilder fileUrlBuilder)
    {
        this.candidates = candidates;
        this.referenceData = referenceData;
        this.unitOfWork = unitOfWork;
        this.fileStorage = fileStorage;
        this.fileValidator = fileValidator;
        this.fileUrlBuilder = fileUrlBuilder;
    }

    /// <inheritdoc/>
    public async Task<CandidateProfileDto> GetAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        Candidate candidate = await this.LoadAsync(candidateId, cancellationToken);
        return await this.ToDtoAsync(candidate, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CandidateProfileDto> UpdateAsync(
        Guid candidateId,
        UpdateCandidateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? desiredRole = TrimToNull(request.DesiredRole);
        string? experienceSummary = TrimToNull(request.ExperienceSummary);
        List<FieldErrorDto> errors = [];

        if (desiredRole is { Length: > DesiredRoleMaxLength })
        {
            errors.Add(new FieldErrorDto("desiredRole", FieldReasons.TextTooLong(DesiredRoleMaxLength)));
        }

        Location? location = null;
        if (request.LocationId == Guid.Empty)
        {
            errors.Add(new FieldErrorDto("locationId", FieldReasons.Required));
        }
        else
        {
            location = await this.referenceData.GetLocationAsync(request.LocationId, cancellationToken);
            if (location is null)
            {
                errors.Add(new FieldErrorDto("locationId", FieldReasons.NotInList));
            }
        }

        if (request.Availability is { } availability && !Enum.IsDefined(availability))
        {
            errors.Add(new FieldErrorDto("availability", FieldReasons.InvalidValue));
        }

        if (experienceSummary is { Length: > ExperienceSummaryMaxLength })
        {
            errors.Add(new FieldErrorDto("experienceSummary", FieldReasons.TextTooLong(ExperienceSummaryMaxLength)));
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        Candidate candidate = await this.LoadAsync(candidateId, cancellationToken);
        candidate.DesiredRole = desiredRole;
        candidate.LocationId = request.LocationId;
        candidate.Availability = request.Availability;
        candidate.ExperienceSummary = experienceSummary;
        await this.unitOfWork.SaveChangesAsync(cancellationToken);

        return this.ToDto(candidate, location!);
    }

    /// <inheritdoc/>
    public async Task<SearchPreferencesDto> UpdatePreferencesAsync(
        Guid candidateId,
        SearchPreferencesDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<WorkMode> workModes = request.WorkModes ?? [];
        IReadOnlyList<ContractType> contractTypes = request.ContractTypes ?? [];
        List<FieldErrorDto> errors = [];

        if (request.MaxDistanceKm is < 1 or > 500)
        {
            errors.Add(new FieldErrorDto("maxDistanceKm", FieldReasons.MaxDistance));
        }

        if (request.MinSalaryExpectation is < 0 or > SalaryMaxValue)
        {
            errors.Add(new FieldErrorDto("minSalaryExpectation", FieldReasons.MinSalary));
        }

        for (int index = 0; index < workModes.Count; index++)
        {
            if (!Enum.IsDefined(workModes[index]))
            {
                errors.Add(new FieldErrorDto($"workModes[{index}]", FieldReasons.InvalidValue));
            }
        }

        for (int index = 0; index < contractTypes.Count; index++)
        {
            if (!Enum.IsDefined(contractTypes[index]))
            {
                errors.Add(new FieldErrorDto($"contractTypes[{index}]", FieldReasons.InvalidValue));
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        Candidate candidate = await this.candidates.GetByIdAsync(candidateId, cancellationToken)
            ?? throw new ElementNotFoundException();
        candidate.MaxDistanceKm = request.MaxDistanceKm;
        candidate.MinSalaryExpectation = request.MinSalaryExpectation is { } salary ? Math.Round(salary, 2) : null;
        candidate.PreferredWorkModes = workModes.Distinct().ToList();
        candidate.PreferredContractTypes = contractTypes.Distinct().ToList();
        await this.unitOfWork.SaveChangesAsync(cancellationToken);

        return ToPreferences(candidate);
    }

    /// <inheritdoc/>
    public async Task<CandidateProfileDto> SetLinksAsync(
        Guid candidateId,
        SetLinksRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Urls is null)
        {
            throw new ValidationFailedException([new FieldErrorDto("urls", FieldReasons.Required)]);
        }

        List<FieldErrorDto> errors = [];
        if (request.Urls.Count > Candidate.MaxLinks)
        {
            errors.Add(new FieldErrorDto("urls", FieldReasons.TooManyLinks));
        }

        // Os endereços ficam tal como indicados, só sem os espaços nas extremidades; não se importa o conteúdo nem se
        // normaliza o endereço (RF012).
        List<string> urls = request.Urls.Select(url => url?.Trim() ?? string.Empty).ToList();
        for (int index = 0; index < urls.Count; index++)
        {
            string? reason = LinkReason(urls, index);
            if (reason is not null)
            {
                errors.Add(new FieldErrorDto($"urls[{index}]", reason));
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        Candidate candidate = await this.LoadAsync(candidateId, cancellationToken);
        candidate.SetLinks(urls);

        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception)
            when (exception.ConstraintName is LinkPositionUniqueIndex or LinkUrlUniqueIndex)
        {
            // Outro pedido do mesmo Candidato gravou as hiperligações entre a leitura e a gravação.
            throw new OperationConflictException(ApiErrorResponse.ConcurrencyConflict());
        }

        return await this.ToDtoAsync(candidate, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> UploadPhotoAsync(Guid candidateId, IFormFile? file, CancellationToken cancellationToken = default)
    {
        string path = await this.ReplaceFileAsync(
            candidateId,
            file,
            FileKind.ProfilePhoto,
            candidate => candidate.ProfilePhotoPath,
            (candidate, newPath) => candidate.ProfilePhotoPath = newPath,
            cancellationToken);
        return this.fileUrlBuilder.ToUrl(path)!;
    }

    /// <inheritdoc/>
    public async Task UploadCvAsync(Guid candidateId, IFormFile? file, CancellationToken cancellationToken = default)
    {
        await this.ReplaceFileAsync(
            candidateId,
            file,
            FileKind.CurriculumVitae,
            candidate => candidate.CvPath,
            (candidate, newPath) => candidate.CvPath = newPath,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<FileContent> GetCvAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        Candidate candidate = await this.candidates.GetByIdAsync(candidateId, cancellationToken)
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

    private static string? TrimToNull(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? LinkReason(List<string> urls, int index)
    {
        string url = urls[index];
        if (url.Length > LinkMaxLength)
        {
            return FieldReasons.TextTooLong(LinkMaxLength);
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return FieldReasons.LinkScheme;
        }

        // O mesmo endereço duas vezes violaria o índice único uq_candidate_link_url: indica-se a repetição.
        return urls.IndexOf(url) < index ? LinkRepeated : null;
    }

    private static SearchPreferencesDto ToPreferences(Candidate candidate)
    {
        return new SearchPreferencesDto(
            candidate.MaxDistanceKm,
            candidate.MinSalaryExpectation,
            candidate.PreferredWorkModes.ToList(),
            candidate.PreferredContractTypes.ToList());
    }

    private async Task<Candidate> LoadAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        return await this.candidates.GetWithProfileAsync(candidateId, cancellationToken)
            ?? throw new ElementNotFoundException();
    }

    private async Task<CandidateProfileDto> ToDtoAsync(Candidate candidate, CancellationToken cancellationToken)
    {
        Location location = await this.referenceData.GetLocationAsync(candidate.LocationId, cancellationToken)
            ?? throw new InvalidOperationException("A localidade do Candidato não existe na lista pré-definida.");
        return this.ToDto(candidate, location);
    }

    /// <summary>
    /// Converte o Candidato no <see cref="CandidateProfileDto"/>, com as competências por ordem alfabética portuguesa.
    /// </summary>
    /// <param name="candidate">Candidato com o perfil completo carregado.</param>
    /// <param name="location">Localidade do Candidato.</param>
    /// <returns>O perfil.</returns>
    private CandidateProfileDto ToDto(Candidate candidate, Location location)
    {
        return new CandidateProfileDto(
            candidate.Id,
            candidate.FullName,
            candidate.User.Email,
            candidate.PhoneNumber,
            candidate.LocationId,
            ReferenceListService.DisplayName(location),
            candidate.DesiredRole,
            candidate.Availability,
            candidate.ExperienceSummary,
            this.fileUrlBuilder.ToUrl(candidate.ProfilePhotoPath),
            candidate.CvPath is not null,
            candidate.Links.Select(link => link.Url).ToList(),
            candidate.Skills
                .Select(skill => new ReferenceItemDto(skill.Id, skill.Name))
                .OrderBy(item => item.Name, Alphabetical)
                .ToList(),
            candidate.CustomSkills
                .Select(customSkill => new CustomSkillDto(customSkill.Id, customSkill.Label))
                .OrderBy(item => item.Label, Alphabetical)
                .ToList(),
            ToPreferences(candidate));
    }

    /// <summary>
    /// Valida e guarda o ficheiro novo, grava o caminho no Candidato e só depois elimina o ficheiro anterior; se a
    /// gravação falhar, elimina o novo e mantém o anterior.
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="file">Ficheiro enviado.</param>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <param name="currentPath">Lê o caminho atual do Candidato.</param>
    /// <param name="setPath">Grava o caminho novo no Candidato.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O caminho relativo do ficheiro novo.</returns>
    private async Task<string> ReplaceFileAsync(
        Guid candidateId,
        IFormFile? file,
        FileKind kind,
        Func<Candidate, string?> currentPath,
        Action<Candidate, string> setPath,
        CancellationToken cancellationToken)
    {
        ValidationResult validation = this.fileValidator.Validate(file, kind);
        if (!validation.IsValid)
        {
            throw new FileRejectedException(validation.Reason!);
        }

        Candidate candidate = await this.candidates.GetByIdAsync(candidateId, cancellationToken)
            ?? throw new ElementNotFoundException();
        string? previousPath = currentPath(candidate);

        string newPath;
        await using (Stream content = file!.OpenReadStream())
        {
            newPath = await this.fileStorage.SaveAsync(content, kind, cancellationToken);
        }

        setPath(candidate, newPath);
        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await this.fileStorage.DeleteAsync(newPath, CancellationToken.None);
            throw;
        }

        if (previousPath is not null)
        {
            await this.TryDeleteAsync(previousPath);
        }

        return newPath;
    }

    private async Task TryDeleteAsync(string path)
    {
        try
        {
            await this.fileStorage.DeleteAsync(path, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // O ficheiro novo já está gravado e associado ao Candidato: falhar a eliminação do anterior não deve falhar o
            // pedido; fica um ficheiro sem referência na pasta de armazenamento.
        }
    }
}
