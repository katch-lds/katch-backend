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
/// Implementação de <see cref="ICompanyAdminService"/> (modelo de classes do backend v02, secções 5.1 e 5.3).
/// </summary>
/// <remarks>
/// As transições de estado são as da <see cref="Company"/> (documentação da API v02, secção 6.11): o serviço verifica-as
/// antes, para responder com o ERR-30 e o estado atual. A mudança de estado da Empresa, o encerramento das conversas
/// abertas (RF075) e a linha do <c>operation_log</c> (RF112) são gravados numa única gravação, que é uma transação. A
/// exploração de vagas lê o estado da Empresa em cada pedido, por isso as vagas da Empresa suspensa deixam de ser
/// apresentadas e voltam a ser depois da reativação sem mais alterações (RF014, RF116).
/// </remarks>
public sealed class CompanyAdminService : ICompanyAdminService
{
    private static readonly StringComparer Alphabetical = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true);

    private readonly ICompanyRepository companies;
    private readonly IJobRepository jobs;
    private readonly IMatchRepository matches;
    private readonly IOperationLogger operationLogger;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyAdminService"/> class.
    /// </summary>
    /// <param name="companies">Repositório das Empresas.</param>
    /// <param name="jobs">Repositório das vagas, para a lista de vagas publicadas.</param>
    /// <param name="matches">Repositório dos matches, para as conversas abertas da Empresa.</param>
    /// <param name="operationLogger">Registo das operações do RF112.</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    /// <param name="clock">Relógio do instante do encerramento das conversas.</param>
    public CompanyAdminService(
        ICompanyRepository companies,
        IJobRepository jobs,
        IMatchRepository matches,
        IOperationLogger operationLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        this.companies = companies;
        this.jobs = jobs;
        this.matches = matches;
        this.operationLogger = operationLogger;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public async Task SuspendAsync(Guid adminId, Guid companyId, CancellationToken cancellationToken = default)
    {
        Company company = await this.LoadAsync(companyId, cancellationToken);
        EnsureStatus(company, CompanyStatus.Approved);

        company.Suspend();

        DateTimeOffset now = this.clock.UtcNow;
        foreach (Match match in await this.matches.ListMatchesByCompanyAsync(company.Id, cancellationToken))
        {
            if (match.ConversationStatus == ConversationStatus.Open)
            {
                match.CloseConversation(closedById: null, ConversationCloseReason.CompanySuspended, now);
            }
        }

        await this.operationLogger.LogAsync(adminId, OperationType.CompanySuspended, company.Id, cancellationToken);
        await this.SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReactivateAsync(Guid adminId, Guid companyId, CancellationToken cancellationToken = default)
    {
        Company company = await this.LoadAsync(companyId, cancellationToken);
        EnsureStatus(company, CompanyStatus.Suspended);

        // As conversas encerradas pela suspensão mantêm-se em consulta (RF075).
        company.Reactivate();

        await this.operationLogger.LogAsync(adminId, OperationType.CompanyReactivated, company.Id, cancellationToken);
        await this.SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<CompanyAdminDto>> ListCompaniesAsync(CancellationToken cancellationToken = default)
    {
        List<CompanyWithCount> all = await this.companies.ListAllWithPublishedCountAsync(cancellationToken);

        return all
            .Select(item => new CompanyAdminDto(item.Company.Id, item.Company.CompanyName, item.Company.Status, item.PublishedJobs))
            .OrderBy(item => item.CompanyName, Alphabetical)
            .ThenBy(item => item.CompanyId)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<List<PublishedJobAdminDto>> ListPublishedJobsAsync(CancellationToken cancellationToken = default)
    {
        List<JobWithCount> published = await this.jobs.ListPublishedWithInterestCountAsync(cancellationToken);

        // O JobWithCount tem só a vaga e a contagem (modelo de classes v02, secção 4); a designação vem das Empresas.
        Dictionary<Guid, string> companyNames = (await this.companies.ListAllWithPublishedCountAsync(cancellationToken))
            .ToDictionary(item => item.Company.Id, item => item.Company.CompanyName);

        // Publicar e voltar a publicar preenchem published_at (Job.Publish, Job.Republish); a data da primeira publicação,
        // que o ck_job_published garante, só é usada se faltar. A ordenação é feita aqui, como nas restantes listas.
        return published
            .Select(item => new PublishedJobAdminDto(
                item.Job.Id,
                item.Job.Title,
                companyNames.GetValueOrDefault(item.Job.CompanyId, string.Empty),
                item.Job.PublishedAt ?? item.Job.FirstPublishedAt ?? DateTimeOffset.MinValue,
                item.InterestCount))
            .OrderByDescending(item => item.PublishedAt)
            .ThenBy(item => item.JobId)
            .ToList();
    }

    private static void EnsureStatus(Company company, CompanyStatus expected)
    {
        if (company.Status != expected)
        {
            throw new OperationConflictException(
                ApiErrorResponse.InvalidState(ApiErrorResponse.CompanyStatusLabel(company.Status)));
        }
    }

    private async Task<Company> LoadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return await this.companies.GetByIdAsync(companyId, cancellationToken)
            ?? throw new ElementNotFoundException();
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Uma das conversas foi alterada por outro pedido entre a leitura e a gravação: nada fica gravado e o
            // Administrador repete a operação (modelo de dados, secção 7.3).
            throw new OperationConflictException(ApiErrorResponse.ConcurrencyConflict());
        }
    }
}
