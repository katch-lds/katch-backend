// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="ICandidateProfileService"/> (modelo de classes do backend v02, secções 5.1 e 5.3). As
/// regras que dependem só do perfil (P07, competência repetida) são do <see cref="Candidate"/>; este serviço confirma a
/// competência na lista, escolhe o motivo de cada erro de campo e grava.
/// </summary>
public sealed class CandidateProfileService : ICandidateProfileService
{
    // Índices que impedem a mesma competência duas vezes no perfil quando dois pedidos chegam ao mesmo tempo.
    private const string SkillPrimaryKey = "pk_candidate_skill";
    private const string CustomSkillUniqueIndex = "uq_candidate_custom_skill";
    private const string CustomSkillLabelCheck = "ck_candidate_custom_skill_label";

    private static readonly StringComparer Alphabetical = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true);

    private readonly ICandidateRepository candidates;
    private readonly IReferenceDataRepository referenceData;
    private readonly IUnitOfWork unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateProfileService"/> class.
    /// </summary>
    /// <param name="candidates">Repositório dos Candidatos.</param>
    /// <param name="referenceData">Repositório das listas pré-definidas.</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    public CandidateProfileService(
        ICandidateRepository candidates,
        IReferenceDataRepository referenceData,
        IUnitOfWork unitOfWork)
    {
        this.candidates = candidates;
        this.referenceData = referenceData;
        this.unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<CandidateProfileDto> AddSkillAsync(
        Guid candidateId,
        AddSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SkillId is not { } skillId)
        {
            throw Invalid("skillId", FieldReasons.Required);
        }

        Candidate candidate = await this.LoadAsync(candidateId, cancellationToken);
        Skill skill = await this.referenceData.GetSkillAsync(skillId, cancellationToken)
            ?? throw Invalid("skillId", FieldReasons.NotInList);

        if (candidate.Skills.Any(item => item.Id == skillId))
        {
            throw Invalid("skillId", FieldReasons.SkillAlreadyAdded);
        }

        candidate.AddSkill(skill);

        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == SkillPrimaryKey)
        {
            throw Invalid("skillId", FieldReasons.SkillAlreadyAdded);
        }

        return await this.ToDtoAsync(candidate, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveSkillAsync(Guid candidateId, Guid skillId, CancellationToken cancellationToken = default)
    {
        Candidate candidate = await this.LoadAsync(candidateId, cancellationToken);

        try
        {
            candidate.RemoveSkill(skillId);
        }
        catch (KeyNotFoundException)
        {
            throw new ElementNotFoundException();
        }

        await this.SaveRemovalAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CandidateProfileDto> AddCustomSkillAsync(
        Guid candidateId,
        AddCustomSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.Label))
        {
            throw Invalid("label", FieldReasons.Required);
        }

        // A validação do P07 é feita aqui, no servidor, e não depende do cliente (RF009). A entidade lança a mesma
        // exceção para o formato e para a repetição; as verificações prévias distinguem os dois motivos.
        string label;
        try
        {
            label = CandidateCustomSkill.Normalize(request.Label);
        }
        catch (ArgumentException)
        {
            throw Invalid("label", FieldReasons.CustomSkillFormat);
        }

        Candidate candidate = await this.LoadAsync(candidateId, cancellationToken);

        if (candidate.CustomSkills.Any(item => string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase)))
        {
            throw Invalid("label", FieldReasons.SkillAlreadyAdded);
        }

        candidate.AddCustomSkill(label);

        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == CustomSkillUniqueIndex)
        {
            throw Invalid("label", FieldReasons.SkillAlreadyAdded);
        }
        catch (CheckConstraintViolationException exception) when (exception.ConstraintName == CustomSkillLabelCheck)
        {
            // O CHECK usa a classe [[:alnum:]], que depende da locale da base: com a locale C, por exemplo, recusa as
            // letras acentuadas que o P07 admite. A recusa chega ao cliente como ERR-10 no campo, e não como ERR-50.
            throw Invalid("label", FieldReasons.CustomSkillFormat);
        }

        return await this.ToDtoAsync(candidate, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveCustomSkillAsync(Guid candidateId, Guid customSkillId, CancellationToken cancellationToken = default)
    {
        Candidate candidate = await this.LoadAsync(candidateId, cancellationToken);

        try
        {
            candidate.RemoveCustomSkill(customSkillId);
        }
        catch (KeyNotFoundException)
        {
            throw new ElementNotFoundException();
        }

        await this.SaveRemovalAsync(cancellationToken);
    }

    /// <summary>
    /// Converte o Candidato no <see cref="CandidateProfileDto"/>, com as competências por ordem alfabética portuguesa.
    /// </summary>
    /// <param name="candidate">Candidato com o perfil completo carregado.</param>
    /// <param name="location">Localidade do Candidato.</param>
    /// <returns>O perfil.</returns>
    internal static CandidateProfileDto ToDto(Candidate candidate, Location location)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(location);

        // photoUrl fica vazio: o URL da fotografia (/api/files/PROFILE_PHOTO/...) depende do carregamento e da obtenção
        // de ficheiros, que são da I113, e nenhuma operação desta versão grava a fotografia de perfil.
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
            PhotoUrl: null,
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
            new SearchPreferencesDto(
                candidate.MaxDistanceKm,
                candidate.MinSalaryExpectation,
                candidate.PreferredWorkModes.ToList(),
                candidate.PreferredContractTypes.ToList()));
    }

    private static ValidationFailedException Invalid(string field, string reason)
    {
        return new ValidationFailedException([new FieldErrorDto(field, reason)]);
    }

    private async Task<Candidate> LoadAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        return await this.candidates.GetWithProfileAsync(candidateId, cancellationToken)
            ?? throw new ElementNotFoundException();
    }

    private async Task SaveRemovalAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Outro pedido retirou a mesma competência entre a leitura e a gravação: o resultado é o de um pedido sobre
            // uma competência que já não está no perfil.
            throw new ElementNotFoundException();
        }
    }

    private async Task<CandidateProfileDto> ToDtoAsync(Candidate candidate, CancellationToken cancellationToken)
    {
        Location location = await this.referenceData.GetLocationAsync(candidate.LocationId, cancellationToken)
            ?? throw new InvalidOperationException("A localidade do Candidato não existe na lista pré-definida.");
        return ToDto(candidate, location);
    }
}
