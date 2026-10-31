// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Api;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Net.Http.Headers;

namespace Katch.Backend.Controllers;

/// <summary>
/// Gestão das vagas pelo Recrutador da Empresa aprovada (documentação da API v02, secção 6.5; modelo de classes do
/// backend v02, secção 6; UC09, UC10). A Empresa é sempre a do Recrutador da credencial de sessão (RNF006, DC-05); uma
/// vaga de outra Empresa dá <c>403</c> (ERR-08, RF059) e uma vaga eliminada <c>404</c> (ERR-20). O carregamento das
/// fotografias (6.5.9) é acrescentado pelo <see cref="IJobService"/>.
/// </summary>
[ApiController]
[Route("api/recruiter/jobs")]
[Authorize(Policy = AuthorizationPolicies.ApprovedCompany)]
[Produces("application/json")]
[ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
public sealed class JobsController : ControllerBase
{
    private const string UploadFileField = "file";

    private readonly IJobService jobService;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobsController"/> class.
    /// </summary>
    /// <param name="jobService">Serviço da gestão das vagas.</param>
    public JobsController(IJobService jobService)
    {
        this.jobService = jobService;
    }

    /// <summary>
    /// Lista as vagas da Empresa, sem as eliminadas, da mais recente para a mais antiga, com os interesses em espera de
    /// cada uma (RF059, RF060).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com as vagas da Empresa.</returns>
    [HttpGet]
    [ProducesResponseType<List<JobDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobDto>>> List(CancellationToken cancellationToken)
    {
        return await this.jobService.ListOwnAsync(this.RecruiterId(), cancellationToken);
    }

    /// <summary>
    /// Cria uma vaga não publicada (UC09). Todos os campos em erro são indicados na mesma resposta (RF050, RF051).
    /// </summary>
    /// <param name="request">Dados da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>201</c> com a vaga, sem <c>Location</c> (não há consulta individual de vaga para o Recrutador), ou <c>400</c>
    /// (ERR-10) com os campos em erro.
    /// </returns>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<JobDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobDto>> Create(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JobRequest? request,
        CancellationToken cancellationToken)
    {
        JobDto job = await this.jobService.CreateAsync(this.RecruiterId(), request ?? JobRequest.Empty, cancellationToken);
        return this.StatusCode(StatusCodes.Status201Created, job);
    }

    /// <summary>
    /// Substitui os dados editáveis de uma vaga, com as validações da criação; o estado não muda (RF054).
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="request">Dados completos da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com a vaga, <c>400</c> (ERR-10), <c>403</c> (ERR-08), <c>404</c> (ERR-20) ou <c>409</c> (ERR-30) se a
    /// vaga estiver encerrada.
    /// </returns>
    [HttpPut("{jobId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobDto>> Update(
        Guid jobId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JobRequest? request,
        CancellationToken cancellationToken)
    {
        return await this.jobService.UpdateAsync(this.RecruiterId(), jobId, request ?? JobRequest.Empty, cancellationToken);
    }

    /// <summary>
    /// Publica uma vaga não publicada (RF053); na primeira publicação fica também a data da primeira publicação (RF096).
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com a vaga, <c>403</c> (ERR-08), <c>404</c> (ERR-20) ou <c>409</c> (ERR-30) fora de não publicada ou
    /// com a data-limite atingida.
    /// </returns>
    [HttpPost("{jobId:guid}/publish")]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobDto>> Publish(Guid jobId, CancellationToken cancellationToken)
    {
        return await this.jobService.PublishAsync(this.RecruiterId(), jobId, cancellationToken);
    }

    /// <summary>
    /// Suspende uma vaga publicada; deixa de ser apresentada aos Candidatos e os interesses mantêm-se (RF055, RF113).
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com a vaga, <c>403</c> (ERR-08), <c>404</c> (ERR-20) ou <c>409</c> (ERR-30) fora de publicada.</returns>
    [HttpPost("{jobId:guid}/suspend")]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobDto>> Suspend(Guid jobId, CancellationToken cancellationToken)
    {
        return await this.jobService.SuspendAsync(this.RecruiterId(), jobId, cancellationToken);
    }

    /// <summary>
    /// Volta a publicar uma vaga suspensa; a data da primeira publicação não muda (RF115).
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com a vaga, <c>403</c> (ERR-08), <c>404</c> (ERR-20) ou <c>409</c> (ERR-30) fora de suspensa.</returns>
    [HttpPost("{jobId:guid}/republish")]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobDto>> Republish(Guid jobId, CancellationToken cancellationToken)
    {
        return await this.jobService.RepublishAsync(this.RecruiterId(), jobId, cancellationToken);
    }

    /// <summary>
    /// Encerra definitivamente uma vaga; os interesses e os matches mantêm-se (RF056, RF113).
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com a vaga, <c>403</c> (ERR-08), <c>404</c> (ERR-20) ou <c>409</c> (ERR-30) se já estiver encerrada.</returns>
    [HttpPost("{jobId:guid}/close")]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobDto>> Close(Guid jobId, CancellationToken cancellationToken)
    {
        return await this.jobService.CloseAsync(this.RecruiterId(), jobId, cancellationToken);
    }

    /// <summary>
    /// Elimina logicamente uma vaga sem interesses registados; deixa de aparecer em todas as consultas (RF058, RF114).
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>204</c>, <c>403</c> (ERR-08), <c>404</c> (ERR-20) ou <c>409</c> (ERR-38) se a vaga tiver pelo menos um
    /// interesse.
    /// </returns>
    [HttpDelete("{jobId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid jobId, CancellationToken cancellationToken)
    {
        await this.jobService.DeleteAsync(this.RecruiterId(), jobId, cancellationToken);
        return this.NoContent();
    }

    /// <summary>
    /// Acrescenta uma fotografia à vaga, em JPEG ou PNG até 5 MB, com o máximo de 5 por vaga (6.5.9; RF052; RNF015,
    /// RNF016; P01, P13). O pedido é <c>multipart/form-data</c> com o campo <c>file</c>.
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>201</c> sem corpo, com o URL da fotografia no cabeçalho <c>Location</c>; <c>400</c> (ERR-11), <c>403</c>
    /// (ERR-08), <c>404</c> (ERR-20), <c>409</c> (ERR-30 se a vaga estiver encerrada, ERR-39 se já tiver 5 fotografias),
    /// <c>413</c> (ERR-12) ou <c>415</c> (ERR-13).
    /// </returns>
    [HttpPost("{jobId:guid}/photos")]
    [UploadRequestFilter(CandidateProfileController.MaxUploadRequestBytes)]
    [RequestSizeLimit(CandidateProfileController.MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = CandidateProfileController.MaxUploadRequestBytes)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> AddPhoto(Guid jobId, CancellationToken cancellationToken)
    {
        IFormCollection form = await this.Request.ReadFormAsync(cancellationToken);
        string url = await this.jobService.AddPhotoAsync(
            this.RecruiterId(),
            jobId,
            form.Files.GetFile(UploadFileField),
            cancellationToken);
        this.Response.Headers[HeaderNames.Location] = url;
        return this.StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Identificador do Recrutador, lido do <c>sub</c> da credencial de sessão (nunca da rota nem do corpo).
    /// </summary>
    private Guid RecruiterId()
    {
        return this.User.TryGetAccountId(out Guid recruiterId)
            ? recruiterId
            : throw new InvalidOperationException("A credencial de sessão não identifica a conta.");
    }
}
