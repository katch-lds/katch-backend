// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Domain;

/// <summary>
/// Vaga publicada por uma Empresa (tabela <c>job</c>), com os dados da oportunidade, as fotografias, a data-limite, o
/// estado de publicação e a eliminação lógica.
/// </summary>
/// <remarks>
/// A entidade aplica as regras que dependem só da própria linha (modelo de dados v02, secções 6.11 e 7.1; modelo de
/// classes do backend v02, secção 3.3) e as transições de estado da documentação da API v02 (secção 6.5):
/// intervalo salarial positivo e coerente, até 5 fotografias, estado inicial <see cref="JobStatus.Unpublished"/> e
/// primeira publicação registada antes de a vaga ficar publicada ou suspensa. Uma vaga eliminada
/// (<see cref="DeletedAt"/>) deixa de poder ser gerida (DM-09) e é excluída das consultas pelo filtro global do
/// <c>KatchDbContext</c>. A condição de eliminação (sem interesses registados), a autoria e a data das operações
/// dependem de outras linhas e ficam nos serviços e no <c>OperationLog</c> (DM-23). As competências pretendidas e os
/// benefícios são as navegações N:M <see cref="Skills"/> e <see cref="Benefits"/>, sobre <c>job_skill</c> e
/// <c>job_benefit</c>; a exigência de pelo menos uma competência (RF050) é verificada pelo serviço.
/// </remarks>
public sealed class Job
{
    /// <summary>
    /// Número máximo de fotografias da vaga (P13).
    /// </summary>
    public const int MaxPhotos = 5;

    /// <summary>
    /// Comprimento máximo da função (DM-12).
    /// </summary>
    public const int MaxTitleLength = 150;

    /// <summary>
    /// Comprimento máximo dos caminhos das fotografias (DM-12).
    /// </summary>
    public const int MaxPathLength = 500;

    /// <summary>
    /// Maior valor que cabe numa coluna <c>numeric(10,2)</c>.
    /// </summary>
    public const decimal MaxSalaryValue = 99_999_999.99m;

    /// <summary>
    /// Initializes a new instance of the <see cref="Job"/> class no estado <see cref="JobStatus.Unpublished"/> (RF050).
    /// </summary>
    /// <param name="companyId">Identificador da Empresa que publica a vaga.</param>
    /// <param name="data">Dados da vaga.</param>
    /// <exception cref="ArgumentException">
    /// Se o identificador da Empresa estiver vazio ou se algum dado não respeitar o modelo de dados.
    /// </exception>
    public Job(Guid companyId, JobData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("O identificador da Empresa é obrigatório.", nameof(companyId));
        }

        Validate(data);

        this.Id = Guid.NewGuid();
        this.CompanyId = companyId;
        this.Title = string.Empty;
        this.Description = string.Empty;
        this.Apply(data);
        this.Status = JobStatus.Unpublished;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Job"/> class. Usado pelo Entity Framework Core.
    /// </summary>
    private Job()
    {
        this.Title = string.Empty;
        this.Description = string.Empty;
    }

    /// <summary>
    /// Gets the identifier of the job (<c>id</c>).
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the identifier of the company that publishes the job (<c>company_id</c>).
    /// </summary>
    public Guid CompanyId { get; private set; }

    /// <summary>
    /// Gets the role (<c>title</c>).
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// Gets the description of the opportunity (<c>description</c>).
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Gets the minimum value of the salary range in euros (<c>min_salary</c>).
    /// </summary>
    public decimal MinSalary { get; private set; }

    /// <summary>
    /// Gets the maximum value of the salary range in euros (<c>max_salary</c>).
    /// </summary>
    public decimal MaxSalary { get; private set; }

    /// <summary>
    /// Gets the identifier of the location of the job (<c>location_id</c>).
    /// </summary>
    public Guid LocationId { get; private set; }

    /// <summary>
    /// Gets the type of contract (<c>contract_type</c>).
    /// </summary>
    public ContractType ContractType { get; private set; }

    /// <summary>
    /// Gets the work mode (<c>work_mode</c>); remote jobs are not subject to the distance filter.
    /// </summary>
    public WorkMode WorkMode { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the job is urgent (<c>is_urgent</c>).
    /// </summary>
    public bool IsUrgent { get; private set; }

    /// <summary>
    /// Gets the paths of the photos of the job (<c>photo_paths</c>), at most 5, in the order in which they are shown.
    /// </summary>
    public List<string> PhotoPaths { get; private set; } = [];

    /// <summary>
    /// Gets the optional publication deadline (<c>expires_at</c>).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// Gets the state of the job (<c>status</c>).
    /// </summary>
    public JobStatus Status { get; private set; }

    /// <summary>
    /// Gets the instant of the first publication (<c>first_published_at</c>), which does not change in a new publication.
    /// </summary>
    public DateTimeOffset? FirstPublishedAt { get; private set; }

    /// <summary>
    /// Gets the instant of the last publication (<c>published_at</c>), the publication date that is shown.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>
    /// Gets the instant of the last suspension (<c>suspended_at</c>).
    /// </summary>
    public DateTimeOffset? SuspendedAt { get; private set; }

    /// <summary>
    /// Gets the instant of the logical deletion by the recruiter (<c>deleted_at</c>).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>
    /// Gets the skills required by the job, from the pre-defined list (<c>job_skill</c>); at least one (RF050).
    /// </summary>
    public ICollection<Skill> Skills { get; private set; } = [];

    /// <summary>
    /// Gets the benefits offered by the job, from the pre-defined list (<c>job_benefit</c>).
    /// </summary>
    public ICollection<Benefit> Benefits { get; private set; } = [];

    /// <summary>
    /// Altera os dados da vaga (RF054). O estado não muda.
    /// </summary>
    /// <param name="data">Novos dados da vaga.</param>
    /// <exception cref="InvalidOperationException">Se a vaga estiver encerrada ou eliminada.</exception>
    /// <exception cref="ArgumentException">Se algum dado não respeitar o modelo de dados.</exception>
    public void Update(JobData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        this.EnsureNotDeleted();

        if (this.Status == JobStatus.Closed)
        {
            throw new InvalidOperationException("Uma vaga encerrada não pode ser alterada.");
        }

        Validate(data);
        this.Apply(data);
    }

    /// <summary>
    /// Publica a vaga (RF053): <see cref="JobStatus.Unpublished"/> → <see cref="JobStatus.Published"/>. Preenche
    /// <see cref="FirstPublishedAt"/> na primeira publicação (RF096) e <see cref="PublishedAt"/>.
    /// </summary>
    /// <param name="now">Instante da publicação, em UTC.</param>
    /// <exception cref="InvalidOperationException">
    /// Se a vaga não estiver por publicar, se estiver eliminada ou se a data-limite já tiver sido atingida.
    /// </exception>
    public void Publish(DateTimeOffset now)
    {
        this.EnsureStatus(JobStatus.Unpublished, "Só uma vaga não publicada pode ser publicada.");

        if (this.IsExpired(now))
        {
            throw new InvalidOperationException("A data-limite da vaga já foi atingida.");
        }

        this.FirstPublishedAt ??= now;
        this.PublishedAt = now;
        this.Status = JobStatus.Published;
    }

    /// <summary>
    /// Suspende a vaga publicada (RF055): <see cref="JobStatus.Published"/> → <see cref="JobStatus.Suspended"/>. Os
    /// interesses em espera não são alterados (RF113).
    /// </summary>
    /// <param name="now">Instante da suspensão, em UTC.</param>
    /// <exception cref="InvalidOperationException">Se a vaga não estiver publicada ou se estiver eliminada.</exception>
    public void Suspend(DateTimeOffset now)
    {
        this.EnsureStatus(JobStatus.Published, "Só uma vaga publicada pode ser suspensa.");

        this.SuspendedAt = now;
        this.Status = JobStatus.Suspended;
    }

    /// <summary>
    /// Volta a publicar a vaga suspensa (RF115): <see cref="JobStatus.Suspended"/> → <see cref="JobStatus.Published"/>,
    /// com <see cref="PublishedAt"/> atualizado e <see cref="FirstPublishedAt"/> inalterado.
    /// </summary>
    /// <param name="now">Instante da nova publicação, em UTC.</param>
    /// <exception cref="InvalidOperationException">
    /// Se a vaga não estiver suspensa, se estiver eliminada ou se a data-limite já tiver sido atingida, como na
    /// primeira publicação (<see cref="Publish"/>).
    /// </exception>
    public void Republish(DateTimeOffset now)
    {
        this.EnsureStatus(JobStatus.Suspended, "Só uma vaga suspensa pode voltar a ser publicada.");

        if (this.IsExpired(now))
        {
            throw new InvalidOperationException("A data-limite da vaga já foi atingida.");
        }

        this.PublishedAt = now;
        this.Status = JobStatus.Published;
    }

    /// <summary>
    /// Encerra a vaga por decisão do Recrutador (RF056), a partir de qualquer estado exceto
    /// <see cref="JobStatus.Closed"/>. O encerramento é definitivo; os interesses e os matches mantêm-se (RF113).
    /// </summary>
    /// <exception cref="InvalidOperationException">Se a vaga já estiver encerrada ou se estiver eliminada.</exception>
    public void Close()
    {
        this.EnsureNotDeleted();

        if (this.Status == JobStatus.Closed)
        {
            throw new InvalidOperationException("A vaga já está encerrada.");
        }

        this.Status = JobStatus.Closed;
    }

    /// <summary>
    /// Encerra a vaga publicada cuja data-limite foi atingida, pela tarefa periódica (RF057).
    /// </summary>
    /// <param name="now">Instante da verificação, em UTC.</param>
    /// <exception cref="InvalidOperationException">
    /// Se a vaga não estiver publicada, se estiver eliminada ou se a data-limite ainda não tiver sido atingida.
    /// </exception>
    public void Expire(DateTimeOffset now)
    {
        this.EnsureStatus(JobStatus.Published, "Só uma vaga publicada é encerrada pela data-limite.");

        if (!this.IsExpired(now))
        {
            throw new InvalidOperationException("A data-limite da vaga ainda não foi atingida.");
        }

        this.Status = JobStatus.Closed;
    }

    /// <summary>
    /// Elimina logicamente a vaga (RF058, RF114). O serviço só a permite sem interesses registados.
    /// </summary>
    /// <param name="now">Instante da eliminação, em UTC.</param>
    /// <exception cref="InvalidOperationException">Se a vaga já estiver eliminada.</exception>
    public void MarkDeleted(DateTimeOffset now)
    {
        this.EnsureNotDeleted();

        this.DeletedAt = now;
    }

    /// <summary>
    /// Acrescenta uma fotografia à vaga (RF052), no fim da lista.
    /// </summary>
    /// <param name="path">Caminho relativo do ficheiro guardado, com no máximo 500 caracteres.</param>
    /// <exception cref="InvalidOperationException">
    /// Se a vaga estiver encerrada ou eliminada, ou se já tiver 5 fotografias (P13).
    /// </exception>
    /// <exception cref="ArgumentException">Se o caminho estiver vazio ou passar dos 500 caracteres.</exception>
    public void AddPhoto(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(path.Length, MaxPathLength);
        this.EnsureNotDeleted();

        if (this.Status == JobStatus.Closed)
        {
            throw new InvalidOperationException("Não se acrescentam fotografias a uma vaga encerrada.");
        }

        if (this.PhotoPaths.Count >= MaxPhotos)
        {
            throw new InvalidOperationException($"A vaga tem no máximo {MaxPhotos} fotografias.");
        }

        this.PhotoPaths.Add(path);
    }

    /// <summary>
    /// Indica se a data-limite foi atingida (RF057).
    /// </summary>
    /// <param name="now">Instante da verificação, em UTC.</param>
    /// <returns><see langword="true"/> se <see cref="ExpiresAt"/> estiver preenchida e for anterior ou igual a <paramref name="now"/>.</returns>
    public bool IsExpired(DateTimeOffset now)
    {
        return this.ExpiresAt.HasValue && this.ExpiresAt.Value <= now;
    }

    /// <summary>
    /// Indica se a vaga pode ser apresentada aos Candidatos (RF014): publicada, não eliminada e não expirada, mesmo que
    /// a tarefa periódica ainda não a tenha encerrado (DM-17). O estado da Empresa é verificado na consulta.
    /// </summary>
    /// <param name="now">Instante da verificação, em UTC.</param>
    /// <returns><see langword="true"/> se a vaga puder ser apresentada.</returns>
    public bool IsVisibleToCandidates(DateTimeOffset now)
    {
        return this.Status == JobStatus.Published
            && this.DeletedAt is null
            && !this.IsExpired(now);
    }

    private static void Validate(JobData data)
    {
        if (string.IsNullOrWhiteSpace(data.Title) || data.Title.Length > MaxTitleLength)
        {
            throw new ArgumentException(
                $"A função é obrigatória e tem no máximo {MaxTitleLength} caracteres.",
                nameof(data));
        }

        if (string.IsNullOrWhiteSpace(data.Description))
        {
            throw new ArgumentException("A descrição da vaga é obrigatória.", nameof(data));
        }

        if (data.MinSalary <= 0 || data.MaxSalary < data.MinSalary || data.MaxSalary > MaxSalaryValue)
        {
            throw new ArgumentException(
                "O intervalo salarial deve ter valores positivos, com o máximo maior ou igual ao mínimo (RF051).",
                nameof(data));
        }

        if (data.LocationId == Guid.Empty)
        {
            throw new ArgumentException("A localidade é obrigatória.", nameof(data));
        }

        if (!Enum.IsDefined(data.ContractType) || !Enum.IsDefined(data.WorkMode))
        {
            throw new ArgumentException(
                "O tipo de contrato e o regime de trabalho pertencem às listas pré-definidas (P09, P16).",
                nameof(data));
        }
    }

    private void Apply(JobData data)
    {
        this.Title = data.Title;
        this.Description = data.Description;
        this.MinSalary = data.MinSalary;
        this.MaxSalary = data.MaxSalary;
        this.LocationId = data.LocationId;
        this.ContractType = data.ContractType;
        this.WorkMode = data.WorkMode;
        this.IsUrgent = data.IsUrgent;
        this.ExpiresAt = data.ExpiresAt;
    }

    private void EnsureNotDeleted()
    {
        if (this.DeletedAt is not null)
        {
            throw new InvalidOperationException("A vaga foi eliminada e já não pode ser gerida.");
        }
    }

    private void EnsureStatus(JobStatus expected, string message)
    {
        this.EnsureNotDeleted();

        if (this.Status != expected)
        {
            throw new InvalidOperationException(message);
        }
    }
}
