// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Controllers;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Testes do <see cref="JobsController"/> (documentação da API v02, secção 6.5) com o serviço substituído por um duplo:
/// cada transição de estado da vaga chega à operação certa do serviço, com o Recrutador da credencial e a vaga da rota.
/// As regras das transições são verificadas nos testes de <see cref="JobService"/> e da entidade vaga.
/// </summary>
public sealed class JobsControllerTests
{
    private readonly Guid recruiterId = Guid.NewGuid();
    private readonly Guid jobId = Guid.NewGuid();
    private readonly ServiceDouble<IJobService> service = new();
    private readonly JobsController controller;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobsControllerTests"/> class.
    /// </summary>
    public JobsControllerTests()
    {
        this.controller = new JobsController(this.service.Service).WithUser(this.recruiterId, AccountRoles.Recruiter);
    }

    /// <summary>
    /// A criação devolve <c>201</c> com a vaga e, sem corpo, o serviço recebe o pedido vazio (6.5.2).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CreateReturnsCreatedWithTheJob()
    {
        var job = ControllerTesting.Sentinel<JobDto>();
        this.service.Returns(nameof(IJobService.CreateAsync), job);

        ActionResult<JobDto> result = await this.controller.Create(null, TestContext.Current.CancellationToken);

        ObjectResult created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Same(job, created.Value);
        ServiceCall call = this.service.SingleCall(nameof(IJobService.CreateAsync));
        Assert.Equal(this.recruiterId, call.Arguments[0]);
        Assert.Same(JobRequest.Empty, call.Arguments[1]);
    }

    /// <summary>
    /// A lista e a alteração devolvem o resultado do serviço; a alteração sem corpo chega como pedido vazio (6.5.1,
    /// 6.5.3).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListAndUpdateReturnTheServiceResult()
    {
        List<JobDto> jobs = [ControllerTesting.Sentinel<JobDto>()];
        var job = ControllerTesting.Sentinel<JobDto>();
        this.service
            .Returns(nameof(IJobService.ListOwnAsync), jobs)
            .Returns(nameof(IJobService.UpdateAsync), job);

        Assert.Same(jobs, (await this.controller.List(TestContext.Current.CancellationToken)).Value);
        Assert.Same(job, (await this.controller.Update(this.jobId, null, TestContext.Current.CancellationToken)).Value);

        ServiceCall update = this.service.Calls[1];
        Assert.Equal(this.jobId, update.Arguments[1]);
        Assert.Same(JobRequest.Empty, update.Arguments[2]);
    }

    /// <summary>
    /// Publicar, suspender, voltar a publicar e encerrar chamam a transição correspondente do serviço para a vaga da rota
    /// e devolvem a vaga no novo estado (RF053, RF055, RF115, RF056).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EachTransitionCallsTheMatchingServiceOperation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var job = ControllerTesting.Sentinel<JobDto>();
        foreach (string method in new[]
        {
            nameof(IJobService.PublishAsync),
            nameof(IJobService.SuspendAsync),
            nameof(IJobService.RepublishAsync),
            nameof(IJobService.CloseAsync),
        })
        {
            this.service.Returns(method, job);
        }

        Assert.Same(job, (await this.controller.Publish(this.jobId, token)).Value);
        Assert.Same(job, (await this.controller.Suspend(this.jobId, token)).Value);
        Assert.Same(job, (await this.controller.Republish(this.jobId, token)).Value);
        Assert.Same(job, (await this.controller.Close(this.jobId, token)).Value);

        Assert.Equal(
            [nameof(IJobService.PublishAsync), nameof(IJobService.SuspendAsync), nameof(IJobService.RepublishAsync), nameof(IJobService.CloseAsync)],
            this.service.Calls.Select(call => call.Method));
        Assert.All(
            this.service.Calls,
            call =>
            {
                Assert.Equal(this.recruiterId, call.Arguments[0]);
                Assert.Equal(this.jobId, call.Arguments[1]);
            });
    }

    /// <summary>
    /// A transição recusada pelo serviço (ERR-30) sobe sem ser convertida no controller: o filtro de exceções trata-a.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectedTransitionPropagatesTheServiceError()
    {
        var conflict = new OperationConflictException(new ApiErrorDto("ERR-30", "Estado inválido."));
        this.service.Throws(nameof(IJobService.CloseAsync), conflict);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.controller.Close(this.jobId, TestContext.Current.CancellationToken));

        Assert.Same(conflict, exception);
    }

    /// <summary>
    /// A eliminação devolve <c>204</c> (6.5.8) e a fotografia acrescentada devolve <c>201</c> com o URL em
    /// <c>Location</c> (6.5.9).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DeleteAndAddPhoto()
    {
        IFormFile? file = this.controller.WithUpload();
        this.service.Returns(nameof(IJobService.AddPhotoAsync), "/api/files/JOB_PHOTO/f.png");

        IActionResult deleted = await this.controller.Delete(this.jobId, TestContext.Current.CancellationToken);
        IActionResult photo = await this.controller.AddPhoto(this.jobId, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(deleted);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<StatusCodeResult>(photo).StatusCode);
        Assert.Equal("/api/files/JOB_PHOTO/f.png", this.controller.Response.Headers[HeaderNames.Location]);
        Assert.Same(file, this.service.Calls[1].Arguments[2]);
    }

    /// <summary>
    /// Uma credencial sem o identificador da conta nunca chega ao serviço.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CredentialWithoutAccountIdNeverReachesTheService()
    {
        this.controller.WithUser(null, AccountRoles.Recruiter);

        await Assert.ThrowsAsync<InvalidOperationException>(() => this.controller.List(TestContext.Current.CancellationToken));

        Assert.Empty(this.service.Calls);
    }
}
