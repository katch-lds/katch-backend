// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IReferenceListService"/> (modelo de classes do backend v02, secções 5.1 e 5.3).
/// </summary>
public sealed class ReferenceListService : IReferenceListService
{
    /// <summary>
    /// Índice único da designação das competências, sobre <c>lower(name)</c> (migration da I049).
    /// </summary>
    internal const string SkillNameUniqueIndex = "uq_skill_name";

    /// <summary>
    /// Índice único da designação dos benefícios, sobre <c>lower(name)</c> (migration da I049).
    /// </summary>
    internal const string BenefitNameUniqueIndex = "uq_benefit_name";

    // Ordem alfabética portuguesa, igual em qualquer servidor: «Évora» fica junto de «Esposende» e não no fim.
    private static readonly StringComparer Alphabetical = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true);

    private readonly IReferenceDataRepository referenceData;
    private readonly IOperationLogger operationLogger;
    private readonly IUnitOfWork unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceListService"/> class.
    /// </summary>
    /// <param name="referenceData">Repositório das listas pré-definidas.</param>
    /// <param name="operationLogger">Registo de autoria das operações relevantes (RF112).</param>
    /// <param name="unitOfWork">Gravação das alterações do pedido.</param>
    public ReferenceListService(
        IReferenceDataRepository referenceData,
        IOperationLogger operationLogger,
        IUnitOfWork unitOfWork)
    {
        this.referenceData = referenceData;
        this.operationLogger = operationLogger;
        this.unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Designação da localidade, com o distrito para distinguir localidades com o mesmo nome (DAPI-08).
    /// </summary>
    /// <param name="location">Localidade.</param>
    /// <returns>A designação «Localidade (Distrito)».</returns>
    public static string DisplayName(Location location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return $"{location.Name} ({location.District})";
    }

    /// <inheritdoc/>
    public async Task<List<ReferenceItemDto>> ListLocationsAsync(CancellationToken cancellationToken = default)
    {
        List<Location> locations = await this.referenceData.ListLocationsAsync(cancellationToken);

        return locations
            .Select(location => new ReferenceItemDto(location.Id, DisplayName(location)))
            .OrderBy(item => item.Name, Alphabetical)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<List<ReferenceItemDto>> ListBenefitsAsync(CancellationToken cancellationToken = default)
    {
        List<Benefit> benefits = await this.referenceData.ListBenefitsAsync(cancellationToken);

        return benefits
            .Select(benefit => new ReferenceItemDto(benefit.Id, benefit.Name))
            .OrderBy(item => item.Name, Alphabetical)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<ReferenceItemDto> AddSkillAsync(
        Guid adminId,
        ReferenceItemRequest request,
        CancellationToken cancellationToken = default)
    {
        string name = await ValidateNameAsync(
            request,
            candidate => this.referenceData.SkillNameExistsAsync(candidate, exceptId: null, cancellationToken));
        var skill = new Skill(name);

        await this.referenceData.AddSkillAsync(skill, cancellationToken);
        await this.operationLogger.LogAsync(adminId, OperationType.SkillCreated, skill.Id, cancellationToken);
        await this.SaveAsync(SkillNameUniqueIndex, cancellationToken);

        return new ReferenceItemDto(skill.Id, skill.Name);
    }

    /// <inheritdoc/>
    public async Task<ReferenceItemDto> RenameSkillAsync(
        Guid adminId,
        Guid skillId,
        ReferenceItemRequest request,
        CancellationToken cancellationToken = default)
    {
        Skill skill = await this.referenceData.GetSkillAsync(skillId, cancellationToken)
            ?? throw new ElementNotFoundException();

        // A própria competência é ignorada: mudar só as maiúsculas («sql» para «SQL») é permitido.
        string name = await ValidateNameAsync(
            request,
            candidate => this.referenceData.SkillNameExistsAsync(candidate, skill.Id, cancellationToken));

        // Só muda a designação: as linhas de candidate_skill e job_skill referem o identificador e ficam como estão.
        skill.Rename(name);
        await this.operationLogger.LogAsync(adminId, OperationType.SkillRenamed, skill.Id, cancellationToken);
        await this.SaveAsync(SkillNameUniqueIndex, cancellationToken);

        return new ReferenceItemDto(skill.Id, skill.Name);
    }

    /// <inheritdoc/>
    public async Task<ReferenceItemDto> AddBenefitAsync(
        Guid adminId,
        ReferenceItemRequest request,
        CancellationToken cancellationToken = default)
    {
        string name = await ValidateNameAsync(
            request,
            candidate => this.referenceData.BenefitNameExistsAsync(candidate, exceptId: null, cancellationToken));
        var benefit = new Benefit(name);

        await this.referenceData.AddBenefitAsync(benefit, cancellationToken);
        await this.operationLogger.LogAsync(adminId, OperationType.BenefitCreated, benefit.Id, cancellationToken);
        await this.SaveAsync(BenefitNameUniqueIndex, cancellationToken);

        return new ReferenceItemDto(benefit.Id, benefit.Name);
    }

    /// <inheritdoc/>
    public async Task<ReferenceItemDto> RenameBenefitAsync(
        Guid adminId,
        Guid benefitId,
        ReferenceItemRequest request,
        CancellationToken cancellationToken = default)
    {
        Benefit benefit = await this.referenceData.GetBenefitAsync(benefitId, cancellationToken)
            ?? throw new ElementNotFoundException();

        string name = await ValidateNameAsync(
            request,
            candidate => this.referenceData.BenefitNameExistsAsync(candidate, benefit.Id, cancellationToken));

        // Só muda a designação: as linhas de job_benefit referem o identificador e ficam como estão.
        benefit.Rename(name);
        await this.operationLogger.LogAsync(adminId, OperationType.BenefitRenamed, benefit.Id, cancellationToken);
        await this.SaveAsync(BenefitNameUniqueIndex, cancellationToken);

        return new ReferenceItemDto(benefit.Id, benefit.Name);
    }

    /// <summary>
    /// Valida a designação pelas regras de 6.13.4: não vazia, até 100 caracteres e única na lista sem distinção de
    /// maiúsculas (RF099 a RF102; DM-12).
    /// </summary>
    /// <param name="request">Pedido com a designação.</param>
    /// <param name="nameExists">Verifica se a designação já existe noutro elemento da lista.</param>
    /// <returns>A designação sem os espaços nas extremidades.</returns>
    private static async Task<string> ValidateNameAsync(ReferenceItemRequest request, Func<string, Task<bool>> nameExists)
    {
        ArgumentNullException.ThrowIfNull(request);

        string name = request.Name?.Trim() ?? string.Empty;
        string? reason = null;

        if (name.Length == 0)
        {
            reason = FieldReasons.ReferenceNameRequired;
        }
        else if (name.Length > Skill.MaxNameLength)
        {
            reason = FieldReasons.TextTooLong(Skill.MaxNameLength);
        }
        else if (await nameExists(name))
        {
            reason = FieldReasons.ReferenceNameTaken;
        }

        return reason is null ? name : throw new ValidationFailedException([new FieldErrorDto("name", reason)]);
    }

    /// <summary>
    /// Grava as alterações; um pedido concorrente com a mesma designação viola o índice único e recebe a mesma resposta
    /// que a designação repetida.
    /// </summary>
    private async Task SaveAsync(string nameUniqueIndex, CancellationToken cancellationToken)
    {
        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == nameUniqueIndex)
        {
            throw new ValidationFailedException([new FieldErrorDto("name", FieldReasons.ReferenceNameTaken)]);
        }
    }
}
