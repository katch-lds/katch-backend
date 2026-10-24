// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Katch.Backend.Controllers;

/// <summary>
/// Registo, criação de conta e início de sessão (documentação da API v02, secção 6.1; modelo de classes do backend
/// v02, secção 6). As restantes operações do controller são acrescentadas pela Issue I054.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Rota do perfil profissional do Candidato, enviada no cabeçalho <c>Location</c> do registo.
    /// </summary>
    internal const string CandidateProfileRoute = "/api/candidate/profile";

    private readonly IAuthService authService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="authService">Serviço de registo e de início de sessão.</param>
    public AuthController(IAuthService authService)
    {
        this.authService = authService;
    }

    /// <summary>
    /// Regista-se como Candidato (UC03): a conta fica ativa, com a quota de 10 interesses, e a sessão iniciada (RF003
    /// a RF005). Todos os campos em erro são indicados na mesma resposta (RF004).
    /// </summary>
    /// <param name="request">Dados do registo.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>201</c> com a credencial de sessão, ou <c>400</c> (ERR-10) com os campos em erro.</returns>
    [HttpPost("candidates")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> RegisterCandidate(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RegisterCandidateRequest? request,
        CancellationToken cancellationToken)
    {
        LoginResponse response = await this.authService.RegisterCandidateAsync(
            request ?? RegisterCandidateRequest.Empty,
            cancellationToken);
        return this.Created(CandidateProfileRoute, response);
    }

    /// <summary>
    /// Cria a conta de Recrutador (UC07): a conta fica ativa e sem Empresa, com a sessão iniciada (RF037). As operações
    /// que exigem a Empresa aprovada devolvem ERR-07 até à aprovação. Todos os campos em erro são indicados na mesma
    /// resposta.
    /// </summary>
    /// <param name="request">Dados da conta.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>201</c> com a credencial de sessão, sem <c>Location</c>, ou <c>400</c> (ERR-10) com os campos em erro.</returns>
    [HttpPost("recruiters")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> CreateRecruiterAccount(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateRecruiterAccountRequest? request,
        CancellationToken cancellationToken)
    {
        LoginResponse response = await this.authService.CreateRecruiterAccountAsync(
            request ?? CreateRecruiterAccountRequest.Empty,
            cancellationToken);

        // Sem Location: o estado da Empresa (6.4.3) só existe depois do registo da Empresa (API v02, secção 4.2).
        return this.StatusCode(StatusCodes.Status201Created, response);
    }
}
