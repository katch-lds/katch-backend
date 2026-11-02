// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.Controllers;

/// <summary>
/// Painel de indicadores do Administrador (documentação da API v02, secção 6.12; modelo de classes do backend v02,
/// secção 6; UC19). Reservado ao Administrador (RF104).
/// </summary>
[ApiController]
[Route("api/admin/indicators")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Produces("application/json")]
public sealed class AdminIndicatorsController : ControllerBase
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IIndicatorService indicatorService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminIndicatorsController"/> class.
    /// </summary>
    /// <param name="indicatorService">Serviço dos indicadores.</param>
    public AdminIndicatorsController(IIndicatorService indicatorService)
    {
        this.indicatorService = indicatorService;
    }

    /// <summary>
    /// Consulta os sete indicadores do RF096 para o período, com as datas no fuso <c>Europe/Lisbon</c> (DAPI-07). A
    /// resposta só tem contagens, sem texto de mensagens (RF103).
    /// </summary>
    /// <param name="from">Data inicial, inclusive, no formato <c>yyyy-MM-dd</c>.</param>
    /// <param name="to">Data final, inclusive, no formato <c>yyyy-MM-dd</c>, não posterior à data atual.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>
    /// <c>200</c> com os indicadores, ou <c>400</c> (ERR-10) com as datas em falta, mal formadas, com a inicial
    /// posterior à final ou com a final posterior à data atual.
    /// </returns>
    [HttpGet]
    [ProducesResponseType<IndicatorsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IndicatorsDto>> Get(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        // As datas chegam como texto para que a falta e o formato errado de cada uma sejam indicados com o campo na
        // mesma resposta; a conversão automática do ASP.NET Core devolvia um 400 sem o campo.
        var errors = new List<FieldErrorDto>();
        DateOnly? start = ParseDate(nameof(from), from, errors);
        DateOnly? end = ParseDate(nameof(to), to, errors);

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        return await this.indicatorService.GetAsync(start!.Value, end!.Value, cancellationToken);
    }

    private static DateOnly? ParseDate(string field, string? value, List<FieldErrorDto> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.Required));
            return null;
        }

        if (!DateOnly.TryParseExact(value.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            errors.Add(new FieldErrorDto(field, FieldReasons.DateFormat));
            return null;
        }

        return date;
    }
}
