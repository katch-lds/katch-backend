// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Katch.Backend.Controllers;

/// <summary>
/// Conversas entre o Candidato e o Recrutador de um match (documentação da API v02, secções 6.8.1 a 6.8.4; modelo de
/// classes do backend v02, secção 6; RF026 a RF030, RF069 a RF075). A conta é sempre a da credencial de sessão (claim
/// <c>sub</c>), nunca a do pedido. O Administrador não tem acesso (RF103). As mensagens novas e as mudanças de estado
/// chegam por consulta periódica (secção 7 da API).
/// </summary>
[ApiController]
[Route("api/conversations")]
[Authorize(Policy = AuthorizationPolicies.CandidateOrRecruiter)]
[Produces("application/json")]
public sealed class ConversationsController : ControllerBase
{
    private readonly IConversationService conversationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationsController"/> class.
    /// </summary>
    /// <param name="conversationService">Serviço das conversas.</param>
    public ConversationsController(IConversationService conversationService)
    {
        this.conversationService = conversationService;
    }

    /// <summary>
    /// Lista as conversas do utilizador, abertas e em modo apenas de consulta, com as mensagens por ler (6.8.1; RF027,
    /// RF029, RF075).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com as conversas, <c>401</c> (ERR-01) sem sessão, ou <c>403</c> (ERR-06) para o Administrador.</returns>
    [HttpGet]
    [ProducesResponseType<List<ConversationSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ConversationSummaryDto>>> List(CancellationToken cancellationToken)
    {
        return await this.conversationService.ListAsync(this.GetAccountId(), cancellationToken);
    }

    /// <summary>
    /// Abre a conversa: devolve as mensagens, da mais antiga para a mais recente, e marca como lidas as da outra parte
    /// (6.8.2; RF026, RF069, RF070). Com <paramref name="after"/>, só as mensagens enviadas depois desse instante.
    /// </summary>
    /// <param name="matchId">Identificador do match.</param>
    /// <param name="after">Instante em ISO 8601; só as mensagens com <c>sentAt</c> posterior.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com as mensagens, <c>400</c> (ERR-10) se <c>after</c> estiver mal formado, <c>403</c> (ERR-08),
    /// <c>404</c> (ERR-20), ou <c>409</c> (ERR-37) se o match não estiver confirmado.
    /// </returns>
    [HttpGet("{matchId:guid}/messages")]
    [ProducesResponseType<List<MessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<List<MessageDto>>> Open(
        Guid matchId,
        [FromQuery] string? after,
        CancellationToken cancellationToken)
    {
        return await this.conversationService.OpenAsync(this.GetAccountId(), matchId, ParseAfter(after), cancellationToken);
    }

    /// <summary>
    /// Envia uma mensagem na conversa (6.8.3; RF030; P03).
    /// </summary>
    /// <param name="matchId">Identificador do match.</param>
    /// <param name="request">Texto da mensagem, de 1 a 1000 caracteres.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>201</c> com a mensagem gravada, <c>400</c> (ERR-10) com o texto inválido, <c>403</c> (ERR-08), <c>404</c>
    /// (ERR-20), ou <c>409</c> (ERR-36) com a conversa em modo apenas de consulta e (ERR-37) sem match confirmado.
    /// </returns>
    [HttpPost("{matchId:guid}/messages")]
    [Consumes("application/json")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageDto>> Send(
        Guid matchId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SendMessageRequest? request,
        CancellationToken cancellationToken)
    {
        MessageDto message = await this.conversationService.SendAsync(
            this.GetAccountId(),
            matchId,
            request ?? SendMessageRequest.Empty,
            cancellationToken);

        // Não há consulta individual de mensagens, por isso a resposta não tem Location.
        return this.StatusCode(StatusCodes.Status201Created, message);
    }

    /// <summary>
    /// Encerra a conversa: passa a modo apenas de consulta para as duas partes, com o histórico visível (6.8.4; RF071,
    /// RF072, RF075).
    /// </summary>
    /// <param name="matchId">Identificador do match.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>204</c> sem corpo, <c>403</c> (ERR-08), <c>404</c> (ERR-20), ou <c>409</c> (ERR-36) se a conversa já estiver
    /// encerrada e (ERR-37) sem match confirmado.
    /// </returns>
    [HttpPost("{matchId:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid matchId, CancellationToken cancellationToken)
    {
        await this.conversationService.CloseAsync(this.GetAccountId(), matchId, cancellationToken);
        return this.NoContent();
    }

    private static DateTimeOffset? ParseAfter(string? after)
    {
        if (after is null)
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(
                after,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new ValidationFailedException([new FieldErrorDto("after", FieldReasons.InvalidValue)]);
        }

        return parsed;
    }

    private Guid GetAccountId()
    {
        return this.User.TryGetAccountId(out Guid accountId)
            ? accountId
            : throw new InvalidOperationException("A credencial de sessão não identifica a conta.");
    }
}
