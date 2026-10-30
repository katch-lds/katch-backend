// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IJobPhotoService"/> (modelo de classes do backend v02, secção 5.1). O ficheiro é gravado
/// depois das verificações da vaga e apagado se a gravação na base de dados falhar, para não ficar órfão.
/// </summary>
public sealed class JobPhotoService : IJobPhotoService
{
    private readonly IJobRepository jobs;
    private readonly ICompanyRepository companies;
    private readonly IFileValidator fileValidator;
    private readonly IFileStorage fileStorage;
    private readonly IFileUrlBuilder fileUrlBuilder;
    private readonly IUnitOfWork unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobPhotoService"/> class.
    /// </summary>
    /// <param name="jobs">Repositório das vagas.</param>
    /// <param name="companies">Repositório das Empresas, para a Empresa do Recrutador.</param>
    /// <param name="fileValidator">Validação do ficheiro enviado.</param>
    /// <param name="fileStorage">Armazenamento dos ficheiros.</param>
    /// <param name="fileUrlBuilder">Construção do URL da fotografia.</param>
    /// <param name="unitOfWork">Gravação das alterações do pedido.</param>
    public JobPhotoService(
        IJobRepository jobs,
        ICompanyRepository companies,
        IFileValidator fileValidator,
        IFileStorage fileStorage,
        IFileUrlBuilder fileUrlBuilder,
        IUnitOfWork unitOfWork)
    {
        this.jobs = jobs;
        this.companies = companies;
        this.fileValidator = fileValidator;
        this.fileStorage = fileStorage;
        this.fileUrlBuilder = fileUrlBuilder;
        this.unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<string> AddPhotoAsync(Guid recruiterId, Guid jobId, IFormFile? file, CancellationToken cancellationToken = default)
    {
        Job job = await this.OwnJobAsync(recruiterId, jobId, cancellationToken);

        if (job.Status == JobStatus.Closed)
        {
            throw new OperationConflictException(ApiErrorResponse.InvalidState(ApiErrorResponse.JobStatusLabel(job.Status)));
        }

        if (job.PhotoPaths.Count >= Job.MaxPhotos)
        {
            throw new OperationConflictException(ApiErrorResponse.PhotoLimitReached(Job.MaxPhotos));
        }

        ValidationResult validation = this.fileValidator.Validate(file, FileKind.JobPhoto);
        if (!validation.IsValid)
        {
            throw new FileRejectedException(validation.Reason!);
        }

        string path;
        await using (Stream content = file!.OpenReadStream())
        {
            path = await this.fileStorage.SaveAsync(content, FileKind.JobPhoto, cancellationToken);
        }

        try
        {
            job.AddPhoto(path);
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await this.fileStorage.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        return this.fileUrlBuilder.ToUrl(path)!;
    }

    private async Task<Job> OwnJobAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken)
    {
        // As vagas eliminadas ficam fora pelo filtro global: 404, mesmo que sejam da própria Empresa.
        Job job = await this.jobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw new ElementNotFoundException();

        // A política ApprovedCompany já garantiu que o Recrutador tem uma Empresa aprovada.
        Company company = await this.companies.GetByRecruiterAsync(recruiterId, cancellationToken)
            ?? throw new InvalidOperationException("O Recrutador não tem Empresa registada.");

        return company.Id == job.CompanyId ? job : throw new ElementForbiddenException();
    }
}
