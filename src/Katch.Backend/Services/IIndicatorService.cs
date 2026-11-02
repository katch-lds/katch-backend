// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Painel de indicadores do Administrador (modelo de classes do backend v02, secção 5.1; UC19).
/// </summary>
public interface IIndicatorService
{
    /// <summary>
    /// Calcula os sete indicadores do RF096 para o período, com as datas no fuso <c>Europe/Lisbon</c>, de <paramref
    /// name="firstDay"/> 00:00 ao fim de <paramref name="lastDay"/> (documentação da API v02, secção 6.12.1; DAPI-07). Só usa
    /// contagens, sem ler o texto das mensagens (RF103).
    /// </summary>
    /// <param name="firstDay">Data inicial, inclusive.</param>
    /// <param name="lastDay">Data final, inclusive.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os indicadores do período.</returns>
    /// <exception cref="ValidationFailedException">Se a data inicial for posterior à final ou a final posterior à data atual.</exception>
    public Task<IndicatorsDto> GetAsync(DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken = default);
}
