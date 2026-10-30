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
/// Implementação de <see cref="IJobService"/> (modelo de classes do backend v02, secções 3.4 e 5.1). As transições de
/// estado são as da entidade <see cref="Job"/>; o serviço valida o pedido, verifica a titularidade, converte as
/// transições recusadas no ERR-30 e regista cada operação no <c>operation_log</c> (RF112).
/// </summary>
public sealed class JobService : IJobService
{
    /// <summary>
    /// Comprimento máximo da descrição no pedido (documentação da API v02, secção 8.3: limite técnico; a coluna é
    /// <c>text</c>).
    /// </summary>
    internal const int MaxDescriptionLength = 5000;

    // Ordem alfabética portuguesa das competências e dos benefícios, como nas listas pré-definidas.
    private static readonly StringComparer Alphabetical = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true);

    private readonly IJobRepository jobs;
    private readonly ICompanyRepository companies;
    private readonly IMatchRepository matches;
    private readonly IReferenceDataRepository referenceData;
    private readonly IOperationLogger operationLogger;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobService"/> class.
    /// </summary>
    /// <param name="jobs">Repositório das vagas.</param>
    /// <param name="companies">Repositório das Empresas, para a Empresa do Recrutador.</param>
    /// <param name="matches">Repositório das respostas às vagas, para os interesses em espera.</param>
    /// <param name="referenceData">Repositório das listas pré-definidas.</param>
    /// <param name="operationLogger">Registo de autoria das operações relevantes (RF112).</param>
    /// <param name="unitOfWork">Gravação das alterações do pedido.</param>
    /// <param name="clock">Relógio, para a data-limite e as datas de publicação.</param>
    public JobService(
        IJobRepository jobs,
        ICompanyRepository companies,
        IMatchRepository matches,
        IReferenceDataRepository referenceData,
        IOperationLogger operationLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        this.jobs = jobs;
        this.companies = companies;
        this.matches = matches;
        this.referenceData = referenceData;
        this.operationLogger = operationLogger;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// URL da API de uma fotografia da vaga (documentação da API v02, secção 6.9; DAPI-13). O caminho guardado tem a
    /// forma <c>&lt;tipo&gt;/&lt;nome&gt;</c>; só o nome vai no URL.
    /// </summary>
    /// <param name="path">Caminho guardado em <c>job.photo_paths</c>.</param>
    /// <returns>O URL, por exemplo <c>/api/files/JOB_PHOTO/3f2a.jpg</c>.</returns>
    public static string PhotoUrl(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return $"/api/files/JOB_PHOTO/{path[(path.LastIndexOf('/') + 1)..]}";
    }

    /// <inheritdoc/>
    public async Task<List<JobDto>> ListOwnAsync(Guid recruiterId, CancellationToken cancellationToken = default)
    {
        Guid companyId = await this.OwnCompanyIdAsync(recruiterId, cancellationToken);
        List<Job> companyJobs = await this.jobs.ListByCompanyAsync(companyId, cancellationToken);
        Dictionary<Guid, int> waiting = await this.matches.CountWaitingByJobAsync(
            companyJobs.Select(job => job.Id).ToList(),
            cancellationToken);
        Dictionary<Guid, string> locationNames = (await this.referenceData.ListLocationsAsync(cancellationToken))
            .ToDictionary(location => location.Id, ReferenceListService.DisplayName);

        return companyJobs
            .Select(job => ToDto(job, locationNames.GetValueOrDefault(job.LocationId, string.Empty), waiting.GetValueOrDefault(job.Id)))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<JobDto> CreateAsync(Guid recruiterId, JobRequest request, CancellationToken cancellationToken = default)
    {
        Guid companyId = await this.OwnCompanyIdAsync(recruiterId, cancellationToken);
        ValidatedJob validated = await this.ValidateAsync(request, cancellationToken);

        var job = new Job(companyId, validated.Data);
        ReplaceLists(job, validated);

        await this.jobs.AddAsync(job, cancellationToken);
        await this.operationLogger.LogAsync(recruiterId, OperationType.JobCreated, job.Id, cancellationToken);
        await this.unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(job, ReferenceListService.DisplayName(validated.Location), waitingCandidates: 0);
    }

    /// <inheritdoc/>
    public async Task<JobDto> UpdateAsync(
        Guid recruiterId,
        Guid jobId,
        JobRequest request,
        CancellationToken cancellationToken = default)
    {
        Job job = await this.OwnJobAsync(recruiterId, jobId, cancellationToken);

        // Uma vaga encerrada não é alterada, mesmo com dados válidos (RF054): o estado é verificado antes dos campos.
        if (job.Status == JobStatus.Closed)
        {
            throw InvalidState(job);
        }

        ValidatedJob validated = await this.ValidateAsync(request, cancellationToken);
        job.Update(validated.Data);
        ReplaceLists(job, validated);

        await this.operationLogger.LogAsync(recruiterId, OperationType.JobUpdated, job.Id, cancellationToken);
        await this.unitOfWork.SaveChangesAsync(cancellationToken);

        return await this.ToDtoAsync(job, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<JobDto> PublishAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default)
    {
        return this.ChangeStatusAsync(
            recruiterId,
            jobId,
            job => job.Publish(this.clock.UtcNow),
            OperationType.JobPublished,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<JobDto> SuspendAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default)
    {
        return this.ChangeStatusAsync(
            recruiterId,
            jobId,
            job => job.Suspend(this.clock.UtcNow),
            OperationType.JobSuspended,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<JobDto> RepublishAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default)
    {
        return this.ChangeStatusAsync(
            recruiterId,
            jobId,
            job => job.Republish(this.clock.UtcNow),
            OperationType.JobRepublished,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<JobDto> CloseAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default)
    {
        return this.ChangeStatusAsync(
            recruiterId,
            jobId,
            job => job.Close(),
            OperationType.JobClosed,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default)
    {
        Job job = await this.OwnJobAsync(recruiterId, jobId, cancellationToken);

        // Só as recusas do Candidato não impedem a eliminação; qualquer interesse obriga a encerrar (RF058).
        if (await this.jobs.HasInterestsAsync(job.Id, cancellationToken))
        {
            throw new OperationConflictException(ApiErrorResponse.JobHasInterests());
        }

        job.MarkDeleted(this.clock.UtcNow);
        await this.operationLogger.LogAsync(recruiterId, OperationType.JobDeleted, job.Id, cancellationToken);
        await this.unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CloseExpiredAsync(CancellationToken cancellationToken = default)
    {
        // Um único instante para toda a execução, do relógio do servidor (UTC); o mesmo é usado pela entidade.
        DateTimeOffset now = this.clock.UtcNow;

        List<Job> expired = await this.jobs.ListExpiredAsync(now, cancellationToken);

        foreach (Job job in expired)
        {
            job.Expire(now);
            await this.operationLogger.LogAsync(null, OperationType.JobClosed, job.Id, cancellationToken);
        }

        // Uma só gravação: as vagas encerradas e os registos ficam todos gravados ou nenhum.
        if (expired.Count > 0)
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return expired.Count;
    }

    private static JobDto ToDto(Job job, string locationName, int waitingCandidates)
    {
        return new JobDto(
            job.Id,
            job.Title,
            job.Description,
            job.MinSalary,
            job.MaxSalary,
            job.LocationId,
            locationName,
            job.ContractType,
            job.WorkMode,
            job.Skills.Select(skill => new ReferenceItemDto(skill.Id, skill.Name)).OrderBy(item => item.Name, Alphabetical).ToList(),
            job.Benefits.Select(benefit => new ReferenceItemDto(benefit.Id, benefit.Name)).OrderBy(item => item.Name, Alphabetical).ToList(),
            job.IsUrgent,
            job.ExpiresAt,
            job.Status,
            job.PublishedAt,
            job.PhotoPaths.Select(PhotoUrl).ToList(),
            waitingCandidates);
    }

    private static void ReplaceLists(Job job, ValidatedJob validated)
    {
        // As associações são substituídas pelas do pedido; competências e benefícios mantêm-se nas listas.
        job.Skills.Clear();
        job.Benefits.Clear();
        validated.Skills.ForEach(job.Skills.Add);
        validated.Benefits.ForEach(job.Benefits.Add);
    }

    private static OperationConflictException InvalidState(Job job)
    {
        return new OperationConflictException(ApiErrorResponse.InvalidState(ApiErrorResponse.JobStatusLabel(job.Status)));
    }

    private static void ValidateText(string field, string value, int maxLength, List<FieldErrorDto> errors)
    {
        if (value.Length == 0)
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.Required));
        }
        else if (value.Length > maxLength)
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.TextTooLong(maxLength)));
        }
    }

    private static bool ValidateSalary(string field, decimal? value, List<FieldErrorDto> errors)
    {
        string? reason = value switch
        {
            null => FieldReasons.Required,
            <= 0 => FieldReasons.SalaryNotPositive,
            > Job.MaxSalaryValue => FieldReasons.SalaryTooHigh,
            decimal amount when decimal.Round(amount, 2) != amount => FieldReasons.SalaryDecimals,
            _ => null,
        };

        if (reason is not null)
        {
            errors.Add(new FieldErrorDto(field, reason));
        }

        return reason is null;
    }

    private static void ValidateEnum<TEnum>(string field, TEnum? value, List<FieldErrorDto> errors)
        where TEnum : struct, Enum
    {
        if (value is null)
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.Required));
        }
        else if (!Enum.IsDefined(value.Value))
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.NotInList));
        }
    }

    /// <summary>
    /// Valida uma lista de identificadores de uma lista pré-definida: cada um tem de existir e não pode ser repetido.
    /// </summary>
    private static async Task<List<TItem>> ValidateListAsync<TItem>(
        string field,
        List<Guid> ids,
        Func<Guid, Task<TItem?>> find,
        List<FieldErrorDto> errors)
        where TItem : class
    {
        var items = new List<TItem>();
        var seen = new HashSet<Guid>();

        for (int index = 0; index < ids.Count; index++)
        {
            Guid id = ids[index];

            if (!seen.Add(id))
            {
                errors.Add(new FieldErrorDto($"{field}[{index}]", FieldReasons.RepeatedInList));
            }
            else if (id == Guid.Empty || await find(id) is not TItem item)
            {
                errors.Add(new FieldErrorDto($"{field}[{index}]", FieldReasons.NotInList));
            }
            else
            {
                items.Add(item);
            }
        }

        return items;
    }

    private async Task<JobDto> ChangeStatusAsync(
        Guid recruiterId,
        Guid jobId,
        Action<Job> transition,
        OperationType operation,
        CancellationToken cancellationToken)
    {
        Job job = await this.OwnJobAsync(recruiterId, jobId, cancellationToken);

        // A entidade recusa as transições fora da tabela de 6.5 (e a publicação com a data-limite atingida).
        try
        {
            transition(job);
        }
        catch (InvalidOperationException)
        {
            throw InvalidState(job);
        }

        await this.operationLogger.LogAsync(recruiterId, operation, job.Id, cancellationToken);
        await this.unitOfWork.SaveChangesAsync(cancellationToken);

        return await this.ToDtoAsync(job, cancellationToken);
    }

    private async Task<Guid> OwnCompanyIdAsync(Guid recruiterId, CancellationToken cancellationToken)
    {
        // A política ApprovedCompany já garantiu que o Recrutador tem uma Empresa aprovada.
        Company company = await this.companies.GetByRecruiterAsync(recruiterId, cancellationToken)
            ?? throw new InvalidOperationException("O Recrutador não tem Empresa registada.");
        return company.Id;
    }

    private async Task<Job> OwnJobAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken)
    {
        // As vagas eliminadas ficam fora pelo filtro global: 404, mesmo que sejam da própria Empresa.
        Job job = await this.jobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw new ElementNotFoundException();

        if (job.CompanyId != await this.OwnCompanyIdAsync(recruiterId, cancellationToken))
        {
            throw new ElementForbiddenException();
        }

        return job;
    }

    private async Task<JobDto> ToDtoAsync(Job job, CancellationToken cancellationToken)
    {
        Location? location = await this.referenceData.GetLocationAsync(job.LocationId, cancellationToken);
        Dictionary<Guid, int> waiting = await this.matches.CountWaitingByJobAsync([job.Id], cancellationToken);

        return ToDto(
            job,
            location is null ? string.Empty : ReferenceListService.DisplayName(location),
            waiting.GetValueOrDefault(job.Id));
    }

    /// <summary>
    /// Valida o pedido pelas regras de 6.5.2 e 8.3 e indica todos os campos em erro na mesma resposta (RF050, RF051).
    /// </summary>
    private async Task<ValidatedJob> ValidateAsync(JobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string title = request.Title?.Trim() ?? string.Empty;
        string description = request.Description?.Trim() ?? string.Empty;
        var errors = new List<FieldErrorDto>();

        ValidateText("title", title, Job.MaxTitleLength, errors);
        ValidateText("description", description, MaxDescriptionLength, errors);
        bool minValid = ValidateSalary("minSalary", request.MinSalary, errors);
        bool maxValid = ValidateSalary("maxSalary", request.MaxSalary, errors);

        if (minValid && maxValid && request.MinSalary > request.MaxSalary)
        {
            errors.Add(new FieldErrorDto("minSalary", FieldReasons.SalaryRange));
        }

        Location? location = null;
        if (request.LocationId == Guid.Empty)
        {
            errors.Add(new FieldErrorDto("locationId", FieldReasons.Required));
        }
        else if ((location = await this.referenceData.GetLocationAsync(request.LocationId, cancellationToken)) is null)
        {
            errors.Add(new FieldErrorDto("locationId", FieldReasons.NotInList));
        }

        ValidateEnum("contractType", request.ContractType, errors);
        ValidateEnum("workMode", request.WorkMode, errors);

        List<Skill> skills = [];
        if (request.SkillIds is null || request.SkillIds.Count == 0)
        {
            errors.Add(new FieldErrorDto("skillIds", FieldReasons.Required));
        }
        else
        {
            skills = await ValidateListAsync(
                "skillIds",
                request.SkillIds,
                id => this.referenceData.GetSkillAsync(id, cancellationToken),
                errors);
        }

        List<Benefit> benefits = await ValidateListAsync(
            "benefitIds",
            request.BenefitIds ?? [],
            id => this.referenceData.GetBenefitAsync(id, cancellationToken),
            errors);

        if (request.ExpiresAt is { } expiresAt && expiresAt <= this.clock.UtcNow)
        {
            errors.Add(new FieldErrorDto("expiresAt", FieldReasons.ExpiresAtNotFuture));
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        var data = new JobData
        {
            Title = title,
            Description = description,
            MinSalary = request.MinSalary!.Value,
            MaxSalary = request.MaxSalary!.Value,
            LocationId = request.LocationId,
            ContractType = request.ContractType!.Value,
            WorkMode = request.WorkMode!.Value,
            IsUrgent = request.IsUrgent ?? false,
            ExpiresAt = request.ExpiresAt,
        };
        return new ValidatedJob(data, location!, skills, benefits);
    }

    private sealed record ValidatedJob(JobData Data, Location Location, List<Skill> Skills, List<Benefit> Benefits);
}
