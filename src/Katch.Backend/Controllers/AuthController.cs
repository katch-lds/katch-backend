// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Katch.Backend.Controllers;

/// <summary>
/// Registo, criação de conta de Recrutador, início e fim de sessão e alteração da palavra-passe (documentação da
/// API v02, secção 6.1; modelo de classes do backend v02, secção 6).
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

    /// <summary>
    /// Cabeçalho do pedido de início de sessão que indica o ponto de acesso (documentação da API v02, secção 3.2).
    /// </summary>
    internal const string ClientAppHeader = "X-Client-App";

    private const string MobileClientApp = "mobile";
    private const string WebClientApp = "web";

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

    /// <summary>
    /// Inicia a sessão (UC01). Verifica, por esta ordem, as credenciais, o estado da conta e o ponto de acesso; o estado
    /// da conta só é revelado a quem apresenta a palavra-passe correta (RF001, RF038, RF083, RNF009). Uma Empresa
    /// pendente, recusada ou suspensa não impede o início de sessão do Recrutador.
    /// </summary>
    /// <param name="clientApp">
    /// Cabeçalho <c>X-Client-App</c>, obrigatório: <c>mobile</c> para o Candidato, <c>web</c> para o Recrutador e o
    /// Administrador.
    /// </param>
    /// <param name="request">Correio eletrónico e palavra-passe.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com a credencial de sessão e o tipo de conta; <c>400</c> (ERR-10) se o cabeçalho, o correio ou a
    /// palavra-passe faltarem ou forem inválidos; <c>401</c> (ERR-02) se as credenciais não coincidirem, com a mesma
    /// mensagem para correio inexistente e palavra-passe errada; <c>403</c> se a conta estiver bloqueada (ERR-03) ou
    /// suspensa (ERR-04), ou se o tipo de conta não puder entrar por este ponto de acesso (ERR-05).
    /// </returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromHeader(Name = ClientAppHeader)] string? clientApp,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LoginRequest? request,
        CancellationToken cancellationToken)
    {
        ClientApp app = ParseClientApp(clientApp);
        LoginResponse response = await this.authService.LoginAsync(request ?? LoginRequest.Empty, app, cancellationToken);
        return this.Ok(response);
    }

    /// <summary>
    /// Termina a sessão (UC01; RF105 a RF107). A autenticação não guarda estado no servidor: o pedido só confirma o fim
    /// da sessão e o cliente descarta a credencial, que continua tecnicamente válida até expirar (documentação da API
    /// v02, secção 3.5).
    /// </summary>
    /// <returns><c>204</c>, ou <c>401</c> (ERR-01) sem credencial válida, ou <c>403</c> (ERR-03, ERR-04) se a conta estiver
    /// bloqueada ou suspensa.</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public IActionResult Logout()
    {
        return this.NoContent();
    }

    /// <summary>
    /// Altera a palavra-passe da própria conta, seja qual for o tipo de conta (UC02; RF002, RF040, RF084). O início de
    /// sessão seguinte só aceita a nova palavra-passe; a credencial atual mantém-se válida até expirar.
    /// </summary>
    /// <param name="request">Palavra-passe atual e palavra-passe nova.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>204</c>, ou <c>400</c> (ERR-10) se a palavra-passe atual estiver errada ou a nova não cumprir o P05, ou
    /// <c>401</c> (ERR-01) sem credencial válida, ou <c>403</c> (ERR-03, ERR-04) se a conta estiver bloqueada ou
    /// suspensa.</returns>
    [HttpPut("password")]
    [Authorize]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangePassword(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChangePasswordRequest? request,
        CancellationToken cancellationToken)
    {
        // O identificador vem sempre da credencial, nunca do pedido (DC-05). Sem ele, o pedido não está autenticado.
        if (!this.User.TryGetAccountId(out Guid userId))
        {
            return this.Unauthorized(ApiErrorResponse.InvalidSession());
        }

        await this.authService.ChangePasswordAsync(userId, request ?? ChangePasswordRequest.Empty, cancellationToken);
        return this.NoContent();
    }

    /// <summary>
    /// Converte o cabeçalho <c>X-Client-App</c> no ponto de acesso. O valor tem de ser exatamente <c>mobile</c> ou
    /// <c>web</c>; um cabeçalho em falta ou com outro valor é recusado com o ERR-10 (documentação da API v02, secção 3.2).
    /// </summary>
    private static ClientApp ParseClientApp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationFailedException([new FieldErrorDto(ClientAppHeader, FieldReasons.Required)]);
        }

        return value switch
        {
            MobileClientApp => ClientApp.Mobile,
            WebClientApp => ClientApp.Web,
            _ => throw new ValidationFailedException([new FieldErrorDto(ClientAppHeader, FieldReasons.InvalidValue)]),
        };
    }
}
