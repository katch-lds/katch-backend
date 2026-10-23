// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="Job"/>: dados da vaga, transições de estado da documentação da API v02 (secção 6.5),
/// fotografias, data-limite e eliminação lógica (modelo de dados v02, secções 6.11 e 7.1; modelo de classes do backend
/// v02, secção 3.3).
/// </summary>
public sealed class JobTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid CompanyId = Guid.NewGuid();

    private static readonly Guid LocationId = Guid.NewGuid();

    /// <summary>
    /// Uma vaga nova fica não publicada, sem datas de publicação, sem fotografias e com os dados recebidos.
    /// </summary>
    [Fact]
    public void NewJobIsUnpublishedWithTheGivenData()
    {
        var job = new Job(CompanyId, Data() with { IsUrgent = true, ExpiresAt = Now.AddDays(30) });

        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(CompanyId, job.CompanyId);
        Assert.Equal("Programador Backend .NET", job.Title);
        Assert.Equal("Desenvolvimento de APIs REST em ASP.NET Core.", job.Description);
        Assert.Equal(1600m, job.MinSalary);
        Assert.Equal(2100m, job.MaxSalary);
        Assert.Equal(LocationId, job.LocationId);
        Assert.Equal(ContractType.Permanent, job.ContractType);
        Assert.Equal(WorkMode.Hybrid, job.WorkMode);
        Assert.True(job.IsUrgent);
        Assert.Equal(Now.AddDays(30), job.ExpiresAt);
        Assert.Equal(JobStatus.Unpublished, job.Status);
        Assert.Empty(job.PhotoPaths);
        Assert.Null(job.FirstPublishedAt);
        Assert.Null(job.PublishedAt);
        Assert.Null(job.SuspendedAt);
        Assert.Null(job.DeletedAt);
    }

    /// <summary>
    /// O mínimo igual ao máximo é um intervalo salarial válido (RF051).
    /// </summary>
    [Fact]
    public void EqualMinimumAndMaximumSalaryIsAccepted()
    {
        var job = new Job(CompanyId, Data() with { MinSalary = 1500m, MaxSalary = 1500m });

        Assert.Equal(job.MinSalary, job.MaxSalary);
    }

    /// <summary>
    /// A Empresa é obrigatória.
    /// </summary>
    [Fact]
    public void EmptyCompanyIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Job(Guid.Empty, Data()));
    }

    /// <summary>
    /// Dados que não respeitam o modelo de dados são rejeitados: função vazia ou acima de 150 caracteres, descrição vazia,
    /// intervalo salarial não positivo, invertido ou acima de <c>numeric(10,2)</c>, localidade vazia e valores fora das
    /// enumerações.
    /// </summary>
    /// <param name="invalid">Nome do caso de dados inválidos.</param>
    [Theory]
    [InlineData("title-empty")]
    [InlineData("title-too-long")]
    [InlineData("description-empty")]
    [InlineData("min-salary-zero")]
    [InlineData("min-salary-negative")]
    [InlineData("max-below-min")]
    [InlineData("max-too-large")]
    [InlineData("location-empty")]
    [InlineData("contract-type-unknown")]
    [InlineData("work-mode-unknown")]
    public void InvalidDataIsRejected(string invalid)
    {
        JobData data = Invalid(invalid);

        Assert.Throws<ArgumentException>(() => new Job(CompanyId, data));
        Assert.Throws<ArgumentException>(() => new Job(CompanyId, Data()).Update(data));
    }

    /// <summary>
    /// A alteração (RF054) muda os dados e mantém o estado, em qualquer estado exceto encerrada.
    /// </summary>
    /// <param name="status">Estado da vaga antes da alteração.</param>
    [Theory]
    [InlineData(JobStatus.Unpublished)]
    [InlineData(JobStatus.Published)]
    [InlineData(JobStatus.Suspended)]
    public void UpdateChangesTheDataAndKeepsTheState(JobStatus status)
    {
        Job job = InState(status);
        Guid newLocation = Guid.NewGuid();

        job.Update(Data() with
        {
            Title = "Analista de Dados",
            MinSalary = 1300m,
            MaxSalary = 1800m,
            LocationId = newLocation,
            ContractType = ContractType.FixedTerm,
            WorkMode = WorkMode.OnSite,
        });

        Assert.Equal("Analista de Dados", job.Title);
        Assert.Equal(1300m, job.MinSalary);
        Assert.Equal(1800m, job.MaxSalary);
        Assert.Equal(newLocation, job.LocationId);
        Assert.Equal(ContractType.FixedTerm, job.ContractType);
        Assert.Equal(WorkMode.OnSite, job.WorkMode);
        Assert.Equal(status, job.Status);
    }

    /// <summary>
    /// Uma vaga encerrada não pode ser alterada.
    /// </summary>
    [Fact]
    public void ClosedJobCannotBeUpdated()
    {
        Job job = InState(JobStatus.Closed);

        Assert.Throws<InvalidOperationException>(() => job.Update(Data()));
    }

    /// <summary>
    /// A publicação (RF053) preenche a primeira publicação e a última publicação.
    /// </summary>
    [Fact]
    public void PublishSetsFirstAndLastPublication()
    {
        var job = new Job(CompanyId, Data());

        job.Publish(Now);

        Assert.Equal(JobStatus.Published, job.Status);
        Assert.Equal(Now, job.FirstPublishedAt);
        Assert.Equal(Now, job.PublishedAt);
    }

    /// <summary>
    /// A publicação é rejeitada com a data-limite já atingida.
    /// </summary>
    [Fact]
    public void PublishWithTheDeadlineReachedIsRejected()
    {
        var job = new Job(CompanyId, Data() with { ExpiresAt = Now });

        Assert.Throws<InvalidOperationException>(() => job.Publish(Now));
        Assert.Equal(JobStatus.Unpublished, job.Status);
        Assert.Null(job.FirstPublishedAt);
    }

    /// <summary>
    /// Só uma vaga não publicada pode ser publicada.
    /// </summary>
    /// <param name="status">Estado da vaga.</param>
    [Theory]
    [InlineData(JobStatus.Published)]
    [InlineData(JobStatus.Suspended)]
    [InlineData(JobStatus.Closed)]
    public void PublishOutsideUnpublishedIsRejected(JobStatus status)
    {
        Assert.Throws<InvalidOperationException>(() => InState(status).Publish(Now));
    }

    /// <summary>
    /// A suspensão (RF055) e a nova publicação (RF115) atualizam a suspensão e a última publicação, mantendo a primeira
    /// publicação.
    /// </summary>
    [Fact]
    public void SuspendAndRepublishKeepTheFirstPublication()
    {
        var job = new Job(CompanyId, Data());
        job.Publish(Now);

        job.Suspend(Now.AddHours(1));

        Assert.Equal(JobStatus.Suspended, job.Status);
        Assert.Equal(Now.AddHours(1), job.SuspendedAt);

        job.Republish(Now.AddHours(2));

        Assert.Equal(JobStatus.Published, job.Status);
        Assert.Equal(Now, job.FirstPublishedAt);
        Assert.Equal(Now.AddHours(2), job.PublishedAt);
        Assert.Equal(Now.AddHours(1), job.SuspendedAt);
    }

    /// <summary>
    /// Só uma vaga publicada pode ser suspensa.
    /// </summary>
    /// <param name="status">Estado da vaga.</param>
    [Theory]
    [InlineData(JobStatus.Unpublished)]
    [InlineData(JobStatus.Suspended)]
    [InlineData(JobStatus.Closed)]
    public void SuspendOutsidePublishedIsRejected(JobStatus status)
    {
        Assert.Throws<InvalidOperationException>(() => InState(status).Suspend(Now));
    }

    /// <summary>
    /// Só uma vaga suspensa pode voltar a ser publicada.
    /// </summary>
    /// <param name="status">Estado da vaga.</param>
    [Theory]
    [InlineData(JobStatus.Unpublished)]
    [InlineData(JobStatus.Published)]
    [InlineData(JobStatus.Closed)]
    public void RepublishOutsideSuspendedIsRejected(JobStatus status)
    {
        Assert.Throws<InvalidOperationException>(() => InState(status).Republish(Now));
    }

    /// <summary>
    /// O encerramento pelo Recrutador (RF056) é admitido a partir de qualquer estado exceto encerrada.
    /// </summary>
    /// <param name="status">Estado da vaga antes do encerramento.</param>
    [Theory]
    [InlineData(JobStatus.Unpublished)]
    [InlineData(JobStatus.Published)]
    [InlineData(JobStatus.Suspended)]
    public void CloseIsAcceptedFromAnyStateButClosed(JobStatus status)
    {
        Job job = InState(status);

        job.Close();

        Assert.Equal(JobStatus.Closed, job.Status);
        Assert.Throws<InvalidOperationException>(job.Close);
    }

    /// <summary>
    /// O encerramento pela data-limite (RF057) só se aplica a vagas publicadas com a data-limite atingida.
    /// </summary>
    [Fact]
    public void ExpireClosesOnlyAPublishedJobWithTheDeadlineReached()
    {
        var job = new Job(CompanyId, Data() with { ExpiresAt = Now.AddDays(1) });
        job.Publish(Now);

        Assert.Throws<InvalidOperationException>(() => job.Expire(Now));

        job.Expire(Now.AddDays(1));

        Assert.Equal(JobStatus.Closed, job.Status);
    }

    /// <summary>
    /// Uma vaga sem data-limite, ou fora do estado publicada, não é encerrada pela tarefa periódica.
    /// </summary>
    [Fact]
    public void ExpireWithoutDeadlineOrOutsidePublishedIsRejected()
    {
        Job withoutDeadline = InState(JobStatus.Published);
        var suspended = new Job(CompanyId, Data() with { ExpiresAt = Now.AddDays(1) });
        suspended.Publish(Now);
        suspended.Suspend(Now);

        Assert.Throws<InvalidOperationException>(() => withoutDeadline.Expire(Now.AddYears(1)));
        Assert.Throws<InvalidOperationException>(() => suspended.Expire(Now.AddDays(2)));
    }

    /// <summary>
    /// A data-limite conta como atingida no próprio instante.
    /// </summary>
    [Fact]
    public void IsExpiredFromTheDeadlineOnwards()
    {
        var job = new Job(CompanyId, Data() with { ExpiresAt = Now });

        Assert.False(job.IsExpired(Now.AddTicks(-1)));
        Assert.True(job.IsExpired(Now));
        Assert.False(new Job(CompanyId, Data()).IsExpired(Now.AddYears(10)));
    }

    /// <summary>
    /// Só as vagas publicadas, não eliminadas e não expiradas são apresentadas aos Candidatos (RF014, DM-17).
    /// </summary>
    [Fact]
    public void OnlyPublishedNotDeletedNotExpiredJobsAreVisible()
    {
        var job = new Job(CompanyId, Data() with { ExpiresAt = Now.AddDays(1) });

        Assert.False(job.IsVisibleToCandidates(Now));

        job.Publish(Now);

        Assert.True(job.IsVisibleToCandidates(Now));
        Assert.False(job.IsVisibleToCandidates(Now.AddDays(1)));

        job.Suspend(Now);

        Assert.False(job.IsVisibleToCandidates(Now));

        job.Republish(Now);
        job.MarkDeleted(Now);

        Assert.False(job.IsVisibleToCandidates(Now));
    }

    /// <summary>
    /// A eliminação lógica (RF058, RF114) preenche <c>deleted_at</c> em qualquer estado; depois, a vaga deixa de poder
    /// ser gerida (DM-09).
    /// </summary>
    /// <param name="status">Estado da vaga antes da eliminação.</param>
    [Theory]
    [InlineData(JobStatus.Unpublished)]
    [InlineData(JobStatus.Published)]
    [InlineData(JobStatus.Suspended)]
    [InlineData(JobStatus.Closed)]
    public void DeletedJobCannotBeManaged(JobStatus status)
    {
        Job job = InState(status);

        job.MarkDeleted(Now);

        Assert.Equal(Now, job.DeletedAt);
        Assert.Equal(status, job.Status);
        Assert.Throws<InvalidOperationException>(() => job.MarkDeleted(Now));
        Assert.Throws<InvalidOperationException>(() => job.Update(Data()));
        Assert.Throws<InvalidOperationException>(() => job.Publish(Now));
        Assert.Throws<InvalidOperationException>(() => job.Suspend(Now));
        Assert.Throws<InvalidOperationException>(() => job.Republish(Now));
        Assert.Throws<InvalidOperationException>(job.Close);
        Assert.Throws<InvalidOperationException>(() => job.Expire(Now));
        Assert.Throws<InvalidOperationException>(() => job.AddPhoto("job-photos/a.jpg"));
    }

    /// <summary>
    /// As fotografias são acrescentadas pela ordem recebida, até 5 (P13).
    /// </summary>
    [Fact]
    public void PhotosAreAddedInOrderUpToFive()
    {
        var job = new Job(CompanyId, Data());
        string[] photos = ["job-photos/1.jpg", "job-photos/2.jpg", "job-photos/3.jpg", "job-photos/4.jpg", "job-photos/5.jpg"];

        foreach (string photo in photos)
        {
            job.AddPhoto(photo);
        }

        Assert.Equal(photos, job.PhotoPaths);
        Assert.Throws<InvalidOperationException>(() => job.AddPhoto("job-photos/6.jpg"));
    }

    /// <summary>
    /// Não se acrescentam fotografias a uma vaga encerrada.
    /// </summary>
    [Fact]
    public void PhotoOnClosedJobIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => InState(JobStatus.Closed).AddPhoto("job-photos/a.jpg"));
    }

    /// <summary>
    /// O caminho da fotografia é obrigatório e tem no máximo 500 caracteres.
    /// </summary>
    [Fact]
    public void InvalidPhotoPathIsRejected()
    {
        var job = new Job(CompanyId, Data());

        Assert.ThrowsAny<ArgumentException>(() => job.AddPhoto(" "));
        Assert.ThrowsAny<ArgumentException>(() => job.AddPhoto(new string('a', Job.MaxPathLength + 1)));
        Assert.Empty(job.PhotoPaths);
    }

    private static JobData Data()
    {
        return new JobData
        {
            Title = "Programador Backend .NET",
            Description = "Desenvolvimento de APIs REST em ASP.NET Core.",
            MinSalary = 1600m,
            MaxSalary = 2100m,
            LocationId = LocationId,
            ContractType = ContractType.Permanent,
            WorkMode = WorkMode.Hybrid,
        };
    }

    private static JobData Invalid(string invalid)
    {
        JobData data = Data();

        return invalid switch
        {
            "title-empty" => data with { Title = "  " },
            "title-too-long" => data with { Title = new string('a', Job.MaxTitleLength + 1) },
            "description-empty" => data with { Description = string.Empty },
            "min-salary-zero" => data with { MinSalary = 0m },
            "min-salary-negative" => data with { MinSalary = -1m },
            "max-below-min" => data with { MaxSalary = 1599.99m },
            "max-too-large" => data with { MaxSalary = Job.MaxSalaryValue + 0.01m },
            "location-empty" => data with { LocationId = Guid.Empty },
            "contract-type-unknown" => data with { ContractType = (ContractType)99 },
            "work-mode-unknown" => data with { WorkMode = (WorkMode)99 },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid)),
        };
    }

    private static Job InState(JobStatus status)
    {
        var job = new Job(CompanyId, Data());

        if (status == JobStatus.Unpublished)
        {
            return job;
        }

        job.Publish(Now);

        if (status == JobStatus.Suspended)
        {
            job.Suspend(Now);
        }
        else if (status == JobStatus.Closed)
        {
            job.Close();
        }

        return job;
    }
}
