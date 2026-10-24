// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Security.Claims;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Katch.Backend.Controllers;

/// <summary>
/// Registo da Empresa e estado do pedido (documentação da API v02, secção 6.4; modelo de classes do backend v02, secção
/// 6). A Empresa é sempre a do Recrutador da credencial de sessão: não há identificador na rota (RNF006, DC-05). As
/// restantes operações do controller (dados de registo, página, logótipo e galeria) são acrescentadas pelas Issues que
/// as implementam.
/// </summary>
[ApiController]
[Route("api/recruiter/company")]
[Authorize(Policy = AuthorizationPolicies.Recruiter)]
[Produces("application/json")]
public sealed class CompanyController : ControllerBase
{
    /// <summary>
    /// Rota da consulta do estado do pedido, enviada no cabeçalho <c>Location</c> do registo.
    /// </summary>
    internal const string StatusRoute = "/api/recruiter/company/status";

    private readonly ICompanyService companyService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyController"/> class.
    /// </summary>
    /// <param name="companyService">Serviço do registo da Empresa.</param>
    public CompanyController(ICompanyService companyService)
    {
        this.companyService = companyService;
    }

    /// <summary>
    /// Regista a Empresa do Recrutador (UC07): com os dados válidos, a Empresa fica pendente de aprovação e a submissão
    /// é registada (RF041 a RF043, RF112). Todos os campos em erro são indicados na mesma resposta (RF042).
    /// </summary>
    /// <param name="request">Dados de registo da Empresa.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>201</c> com o estado do pedido, <c>400</c> (ERR-10) com os campos em erro, ou <c>409</c> (ERR-40) se o
    /// Recrutador já registou uma Empresa.
    /// </returns>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CompanyStatusDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyStatusDto>> Register(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CompanyRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        CompanyStatusDto status = await this.companyService.RegisterAsync(
            this.RecruiterId(),
            request ?? CompanyRegistrationRequest.Empty,
            cancellationToken);
        return this.Created(StatusRoute, status);
    }

    /// <summary>
    /// Corrige os dados do pedido recusado e submete-o novamente (UC07): a Empresa volta a pendente, o motivo da recusa
    /// é limpo e a data de submissão é atualizada (RF045, RF112).
    /// </summary>
    /// <param name="request">Dados de registo corrigidos, completos.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com o estado do pedido, <c>400</c> (ERR-10) com os campos em erro, <c>404</c> (ERR-20) sem Empresa
    /// registada, ou <c>409</c> (ERR-30) se a Empresa não estiver recusada.
    /// </returns>
    [HttpPost("resubmission")]
    [Consumes("application/json")]
    [ProducesResponseType<CompanyStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyStatusDto>> Resubmit(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CompanyRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        return await this.companyService.ResubmitAsync(
            this.RecruiterId(),
            request ?? CompanyRegistrationRequest.Empty,
            cancellationToken);
    }

    /// <summary>
    /// Consulta o estado do pedido de registo, com o motivo da recusa no estado recusado (RF044). É o que a área de
    /// gestão web consulta enquanto as operações reservadas estão vedadas (RF039).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o estado do pedido, ou <c>404</c> (ERR-20) se o Recrutador ainda não registou a Empresa.</returns>
    [HttpGet("status")]
    [ProducesResponseType<CompanyStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return await this.companyService.GetStatusAsync(this.RecruiterId(), cancellationToken);
    }

    /// <summary>
    /// Identificador do Recrutador, lido do <c>sub</c> da credencial de sessão (nunca da rota nem do corpo).
    /// </summary>
    private Guid RecruiterId()
    {
        return Guid.TryParse(this.User.FindFirstValue("sub"), out Guid recruiterId)
            ? recruiterId
            : throw new InvalidOperationException("A credencial de sessão não identifica a conta.");
    }
}
