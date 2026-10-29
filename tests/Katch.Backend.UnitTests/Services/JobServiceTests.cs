// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes de <see cref="JobService"/> (documentação da API v02, secção 6.5; modelo de classes do backend v02,
/// <c>IJobService</c>): limites da validação, titularidade, conversão das transições recusadas no ERR-30, uso do relógio
/// e composição do <see cref="JobDto"/>. Os pedidos HTTP completos são verificados nos testes de integração.
/// </summary>
public sealed class JobServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);

    private readonly Guid recruiterId = Guid.NewGuid();
    private readonly Location location = new("Braga", "Braga", 41.545m, -8.426m);
    private readonly Skill csharp = new("C#");
    private readonly Skill sql = new("SQL");
    private readonly Benefit health = new("Seguro de saúde");
    private readonly FakeStore store = new();
    private readonly FakeOperationLogger operationLogger = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly Company company;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobServiceTests"/> class.
    /// </summary>
    public JobServiceTests()
    {
        this.company = NewApprovedCompany(this.recruiterId, this.location.Id);
        this.store.Companies.Add(this.company);
        this.store.Locations.Add(this.location);
        this.store.Skills.AddRange([this.csharp, this.sql]);
        this.store.Benefits.Add(this.health);
    }

    /// <summary>
    /// A vaga criada fica não publicada, com os dados sem os espaços nas extremidades, as listas por ordem alfabética,
    /// sem benefícios quando a lista vem ausente e não urgente por omissão; é gravada e registada uma vez.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CreateBuildsAnUnpublishedJobWithDefaults()
    {
        JobRequest request = this.Valid() with
        {
            Title = "  Programador  ",
            SkillIds = [this.sql.Id, this.csharp.Id],
            BenefitIds = null,
            IsUrgent = null,
        };

        JobDto job = await this.Service().CreateAsync(this.recruiterId, request, TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Unpublished, job.Status);
        Assert.Equal("Programador", job.Title);
        Assert.Equal("Braga (Braga)", job.LocationName);
        Assert.Equal(["C#", "SQL"], job.Skills.Select(skill => skill.Name));
        Assert.Empty(job.Benefits);
        Assert.False(job.IsUrgent);
        Assert.Null(job.PublishedAt);
        Assert.Equal(0, job.WaitingCandidates);
        Job stored = Assert.Single(this.store.Jobs);
        Assert.Equal(this.company.Id, stored.CompanyId);
        Assert.Equal([(this.recruiterId, OperationType.JobCreated, stored.Id)], this.operationLogger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Todos os campos em erro vêm na mesma resposta, pela ordem do formulário (RF050).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AllInvalidFieldsAreReportedTogether()
    {
        var request = new JobRequest(
            " ",
            new string('d', JobService.MaxDescriptionLength + 1),
            -1m,
            null,
            Guid.NewGuid(),
            (ContractType)99,
            null,
            [this.csharp.Id, Guid.Empty, this.csharp.Id],
            [Guid.NewGuid()],
            true,
            Now);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().CreateAsync(this.recruiterId, request, TestContext.Current.CancellationToken));

        Assert.Equal(
            [
                new FieldErrorDto("title", FieldReasons.Required),
                new FieldErrorDto("description", FieldReasons.TextTooLong(5000)),
                new FieldErrorDto("minSalary", FieldReasons.SalaryNotPositive),
                new FieldErrorDto("maxSalary", FieldReasons.Required),
                new FieldErrorDto("locationId", FieldReasons.NotInList),
                new FieldErrorDto("contractType", FieldReasons.NotInList),
                new FieldErrorDto("workMode", FieldReasons.Required),
                new FieldErrorDto("skillIds[1]", FieldReasons.NotInList),
                new FieldErrorDto("skillIds[2]", FieldReasons.RepeatedInList),
                new FieldErrorDto("benefitIds[0]", FieldReasons.NotInList),
                new FieldErrorDto("expiresAt", FieldReasons.ExpiresAtNotFuture),
            ],
            exception.Fields);
        Assert.Empty(this.store.Jobs);
        Assert.Empty(this.operationLogger.Entries);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Limites aceites: título com 150 caracteres, valores iguais, valor máximo da coluna e data-limite um instante
    /// depois de agora.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BoundaryValuesAreAccepted()
    {
        JobRequest request = this.Valid() with
        {
            Title = new string('t', Job.MaxTitleLength),
            Description = new string('d', JobService.MaxDescriptionLength),
            MinSalary = Job.MaxSalaryValue,
            MaxSalary = Job.MaxSalaryValue,
            ExpiresAt = Now.AddTicks(1),
        };

        JobDto job = await this.Service().CreateAsync(this.recruiterId, request, TestContext.Current.CancellationToken);

        Assert.Equal(Job.MaxSalaryValue, job.MinSalary);
        Assert.Equal(Now.AddTicks(1), job.ExpiresAt);
    }

    /// <summary>
    /// Valores com até 2 casas decimais são aceites e guardados como foram enviados (documentação da API v02, secção
    /// 2.3).
    /// </summary>
    /// <param name="amount">Valor mínimo enviado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("1600")]
    [InlineData("1600.5")]
    [InlineData("1600.50")]
    [InlineData("1600.99")]
    public async Task SalaryWithUpToTwoDecimalsIsAccepted(string amount)
    {
        decimal min = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        JobDto job = await this.Service().CreateAsync(
            this.recruiterId,
            this.Valid() with { MinSalary = min },
            TestContext.Current.CancellationToken);

        Assert.Equal(min, job.MinSalary);
    }

    /// <summary>
    /// Valores com mais de 2 casas decimais são recusados com o campo, em vez de arredondados em silêncio.
    /// </summary>
    /// <param name="field">Campo alterado.</param>
    /// <param name="amount">Valor enviado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("minSalary", "1600.005")]
    [InlineData("minSalary", "1600.001")]
    [InlineData("maxSalary", "2100.999")]
    public async Task SalaryWithMoreThanTwoDecimalsIsRejected(string field, string amount)
    {
        decimal value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        JobRequest request = field == "minSalary"
            ? this.Valid() with { MinSalary = value }
            : this.Valid() with { MaxSalary = value };

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().CreateAsync(this.recruiterId, request, TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto(field, FieldReasons.SalaryDecimals)], exception.Fields);
        Assert.Empty(this.store.Jobs);
    }

    /// <summary>
    /// Voltar a publicar uma vaga suspensa com a data-limite já atingida dá ERR-30 com o estado atual, sem gravar nem
    /// registar nada.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SuspendedJobPastItsDeadlineIsNotRepublished()
    {
        var job = new Job(
            this.company.Id,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = this.location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
                ExpiresAt = Now.AddDays(1),
            });
        job.Publish(Now);
        job.Suspend(Now.AddHours(1));
        this.store.Jobs.Add(job);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.Service(new FixedClock(Now.AddDays(2))).RepublishAsync(this.recruiterId, job.Id, TestContext.Current.CancellationToken));

        Assert.Equal(ApiErrorResponse.InvalidStateCode, exception.Error.Code);
        Assert.Equal("A operação não é permitida no estado atual: suspensa.", exception.Error.Message);
        Assert.Equal(JobStatus.Suspended, job.Status);
        Assert.Empty(this.operationLogger.Entries);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Limites recusados: título com 151 caracteres, valor acima do máximo da coluna e intervalo incoerente.
    /// </summary>
    /// <param name="title">Função.</param>
    /// <param name="min">Valor mínimo.</param>
    /// <param name="max">Valor máximo.</param>
    /// <param name="expected">Campo e motivo esperados, separados por <c>|</c>.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(151, 1000, 2000, "title|O texto não pode exceder 150 caracteres.")]
    [InlineData(10, 1000, 100_000_000, "maxSalary|O valor não pode exceder 99 999 999,99 euros.")]
    [InlineData(10, 0.01, 0, "maxSalary|O valor tem de ser um número positivo em euros.")]
    [InlineData(10, 2000.01, 2000, "minSalary|O valor mínimo não pode ser superior ao valor máximo.")]
    public async Task BoundaryValuesAreRejected(int title, double min, double max, string expected)
    {
        JobRequest request = this.Valid() with
        {
            Title = new string('t', title),
            MinSalary = (decimal)min,
            MaxSalary = (decimal)max,
        };

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().CreateAsync(this.recruiterId, request, TestContext.Current.CancellationToken));

        string[] parts = expected.Split('|');
        Assert.Equal([new FieldErrorDto(parts[0], parts[1])], exception.Fields);
    }

    /// <summary>
    /// Uma data-limite igual ao instante atual é recusada: tem de ser posterior.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ExpiresAtEqualToNowIsRejected()
    {
        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().CreateAsync(this.recruiterId, this.Valid() with { ExpiresAt = Now }, TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("expiresAt", FieldReasons.ExpiresAtNotFuture)], exception.Fields);
    }

    /// <summary>
    /// Uma vaga inexistente dá 404 antes de se verificar a titularidade; uma vaga de outra Empresa dá 403 (RF059).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownJobIsNotFoundAndForeignJobIsForbidden()
    {
        Company other = NewApprovedCompany(Guid.NewGuid(), this.location.Id);
        Job foreign = this.AddJob(other.Id);

        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.Service().PublishAsync(this.recruiterId, Guid.NewGuid(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ElementForbiddenException>(
            () => this.Service().PublishAsync(this.recruiterId, foreign.Id, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ElementForbiddenException>(
            () => this.Service().UpdateAsync(this.recruiterId, foreign.Id, JobRequest.Empty, TestContext.Current.CancellationToken));
        Assert.Equal(JobStatus.Unpublished, foreign.Status);
        Assert.Empty(this.operationLogger.Entries);
    }

    /// <summary>
    /// A publicação usa o relógio do servidor para a primeira publicação e a mais recente; voltar a publicar só muda a
    /// mais recente (RF053, RF115, RF096).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task PublishingUsesTheServerClock()
    {
        Job job = this.AddJob(this.company.Id);
        var clock = new FixedClock(Now);
        JobService service = this.Service(clock);

        await service.PublishAsync(this.recruiterId, job.Id, TestContext.Current.CancellationToken);
        clock.UtcNow = Now.AddDays(1);
        await service.SuspendAsync(this.recruiterId, job.Id, TestContext.Current.CancellationToken);
        clock.UtcNow = Now.AddDays(2);
        JobDto republished = await service.RepublishAsync(this.recruiterId, job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(Now, job.FirstPublishedAt);
        Assert.Equal(Now.AddDays(2), republished.PublishedAt);
        Assert.Equal(
            [OperationType.JobPublished, OperationType.JobSuspended, OperationType.JobRepublished],
            this.operationLogger.Entries.Select(entry => entry.Operation));
    }

    /// <summary>
    /// A transição recusada pela entidade é convertida no ERR-30 com o estado atual, sem gravar nem registar.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectedTransitionBecomesInvalidState()
    {
        Job job = this.AddJob(this.company.Id);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.Service().SuspendAsync(this.recruiterId, job.Id, TestContext.Current.CancellationToken));

        Assert.Equal(ApiErrorResponse.InvalidStateCode, exception.Error.Code);
        Assert.Equal("A operação não é permitida no estado atual: não publicada.", exception.Error.Message);
        Assert.Empty(this.operationLogger.Entries);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// A alteração de uma vaga encerrada dá ERR-30 mesmo com um pedido inválido: o estado é verificado primeiro.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdatingAClosedJobIsRejectedBeforeValidation()
    {
        Job job = this.AddJob(this.company.Id);
        job.Close();

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.Service().UpdateAsync(this.recruiterId, job.Id, JobRequest.Empty, TestContext.Current.CancellationToken));

        Assert.Equal("A operação não é permitida no estado atual: encerrada.", exception.Error.Message);
    }

    /// <summary>
    /// A alteração substitui as competências e os benefícios pelos do pedido e mantém o estado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UpdateReplacesTheLists()
    {
        Job job = this.AddJob(this.company.Id);
        job.Benefits.Add(this.health);

        JobDto updated = await this.Service().UpdateAsync(
            this.recruiterId,
            job.Id,
            this.Valid() with { SkillIds = [this.sql.Id], BenefitIds = [] },
            TestContext.Current.CancellationToken);

        Assert.Equal([this.sql], job.Skills);
        Assert.Empty(job.Benefits);
        Assert.Equal(JobStatus.Unpublished, updated.Status);
        Assert.Equal([(this.recruiterId, OperationType.JobUpdated, job.Id)], this.operationLogger.Entries);
    }

    /// <summary>
    /// Com interesses, a eliminação dá ERR-38 e a vaga fica como estava; sem interesses, fica com a data da eliminação
    /// do relógio do servidor e é registada (RF058, RF114).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DeleteDependsOnInterests()
    {
        Job withInterests = this.AddJob(this.company.Id);
        Job withoutInterests = this.AddJob(this.company.Id);
        this.store.JobsWithInterests.Add(withInterests.Id);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.Service().DeleteAsync(this.recruiterId, withInterests.Id, TestContext.Current.CancellationToken));
        await this.Service().DeleteAsync(this.recruiterId, withoutInterests.Id, TestContext.Current.CancellationToken);

        Assert.Equal(ApiErrorResponse.JobHasInterestsCode, exception.Error.Code);
        Assert.Null(withInterests.DeletedAt);
        Assert.Equal(Now, withoutInterests.DeletedAt);
        Assert.Equal([(this.recruiterId, OperationType.JobDeleted, withoutInterests.Id)], this.operationLogger.Entries);
    }

    /// <summary>
    /// A lista usa a ordem do repositório e junta a designação da localidade, os interesses em espera e os URLs das
    /// fotografias.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListComposesEachJob()
    {
        Job first = this.AddJob(this.company.Id);
        Job second = this.AddJob(this.company.Id);
        second.AddPhoto("job-photos/3f2a.jpg");
        this.store.Waiting[second.Id] = 3;
        this.AddJob(NewApprovedCompany(Guid.NewGuid(), this.location.Id).Id);

        List<JobDto> jobs = await this.Service().ListOwnAsync(this.recruiterId, TestContext.Current.CancellationToken);

        Assert.Equal([first.Id, second.Id], jobs.Select(job => job.JobId));
        Assert.Equal([0, 3], jobs.Select(job => job.WaitingCandidates));
        Assert.All(jobs, job => Assert.Equal("Braga (Braga)", job.LocationName));
        Assert.Equal(["/api/files/JOB_PHOTO/3f2a.jpg"], jobs[1].PhotoUrls);
    }

    /// <summary>
    /// O URL da fotografia usa o tipo da rota de 6.9 e só o nome do ficheiro, nunca o caminho no servidor.
    /// </summary>
    /// <param name="path">Caminho guardado.</param>
    /// <param name="url">URL esperado.</param>
    [Theory]
    [InlineData("job-photos/a.png", "/api/files/JOB_PHOTO/a.png")]
    [InlineData("a.png", "/api/files/JOB_PHOTO/a.png")]
    public void PhotoUrlUsesOnlyTheFileName(string path, string url)
    {
        Assert.Equal(url, JobService.PhotoUrl(path));
    }

    private static Company NewApprovedCompany(Guid recruiterId, Guid locationId)
    {
        var company = new Company(
            recruiterId,
            new CompanyRegistration
            {
                CompanyName = "Lumen Software, Lda.",
                TaxId = "509123456",
                Industry = Industry.Technology,
                Address = "Rua de Exemplo, 1, 4700-000 Braga",
                LocationId = locationId,
                ContactEmail = "geral@lumen-exemplo.pt",
                ContactPhone = "253000000",
                ResponsibleName = "Responsável Exemplo",
            },
            Now);
        company.Approve();
        return company;
    }

    private JobRequest Valid()
    {
        return new JobRequest(
            "Programador Backend .NET",
            "Desenvolvimento de APIs REST.",
            1600m,
            2100m,
            this.location.Id,
            ContractType.Permanent,
            WorkMode.Hybrid,
            [this.csharp.Id],
            [this.health.Id],
            true,
            Now.AddDays(30));
    }

    private Job AddJob(Guid companyId)
    {
        var job = new Job(
            companyId,
            new JobData
            {
                Title = "Programador",
                Description = "Descrição.",
                MinSalary = 1000m,
                MaxSalary = 2000m,
                LocationId = this.location.Id,
                ContractType = ContractType.Permanent,
                WorkMode = WorkMode.Remote,
            });
        job.Skills.Add(this.csharp);
        this.store.Jobs.Add(job);
        return job;
    }

    private JobService Service(IClock? clock = null)
    {
        return new JobService(
            this.store,
            this.store,
            this.store,
            this.store,
            this.operationLogger,
            this.unitOfWork,
            clock ?? new FixedClock(Now));
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now)
        {
            this.UtcNow = now;
        }

        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeStore : IJobRepository, ICompanyRepository, IMatchRepository, IReferenceDataRepository
    {
        public List<Job> Jobs { get; } = [];

        public List<Company> Companies { get; } = [];

        public List<Location> Locations { get; } = [];

        public List<Skill> Skills { get; } = [];

        public List<Benefit> Benefits { get; } = [];

        public HashSet<Guid> JobsWithInterests { get; } = [];

        public Dictionary<Guid, int> Waiting { get; } = [];

        public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Jobs.Find(job => job.Id == id && job.DeletedAt is null));
        }

        public Task AddAsync(Job job, CancellationToken cancellationToken = default)
        {
            this.Jobs.Add(job);
            return Task.CompletedTask;
        }

        public Task<List<Job>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Jobs.Where(job => job.CompanyId == companyId && job.DeletedAt is null).ToList());
        }

        public Task<bool> HasInterestsAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.JobsWithInterests.Contains(jobId));
        }

        public Task<Dictionary<Guid, int>> CountWaitingByJobAsync(IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Waiting.Where(item => jobIds.Contains(item.Key)).ToDictionary());
        }

        public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Companies.Find(item => item.RecruiterId == recruiterId));
        }

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Locations.ToList());
        }

        public Task<Location?> GetLocationAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Locations.Find(item => item.Id == id));
        }

        public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Skills.Find(item => item.Id == id));
        }

        public Task<Benefit?> GetBenefitAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Benefits.Find(item => item.Id == id));
        }

        public Task<JobWithCompanyStatus?> GetWithCompanyStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Match?> GetAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Match match, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeOperationLogger : IOperationLogger
    {
        public List<(Guid? UserId, OperationType Operation, Guid EntityId)> Entries { get; } = [];

        public Task LogAsync(
            Guid? userId,
            OperationType operation,
            Guid entityId,
            CancellationToken cancellationToken = default)
        {
            this.Entries.Add((userId, operation, entityId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            this.Saves++;
            return Task.FromResult(1);
        }

        public void DiscardChanges()
        {
            throw new NotSupportedException();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
