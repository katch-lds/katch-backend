// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Dados de uma vaga criada ou alterada pelo Recrutador (documentação da API v02, secções 6.5.2, 6.5.3 e 8.3; modelo de
/// classes do backend v02, secção 7). Os campos são anuláveis para que um campo em falta chegue ao serviço e seja
/// indicado na mesma resposta que os restantes (RF050).
/// </summary>
/// <param name="Title">Função, até 150 caracteres.</param>
/// <param name="Description">Descrição, até 5000 caracteres.</param>
/// <param name="MinSalary">Valor mínimo do intervalo salarial, positivo, em euros brutos mensais.</param>
/// <param name="MaxSalary">Valor máximo do intervalo salarial, maior ou igual ao mínimo.</param>
/// <param name="LocationId">Localidade da lista pré-definida.</param>
/// <param name="ContractType">Tipo de contrato (P09).</param>
/// <param name="WorkMode">Regime de trabalho (P16).</param>
/// <param name="SkillIds">Competências da lista, pelo menos uma e sem repetidos.</param>
/// <param name="BenefitIds">Benefícios da lista, sem repetidos; ausente equivale a nenhum.</param>
/// <param name="IsUrgent">Indicação de vaga urgente; ausente equivale a <see langword="false"/>.</param>
/// <param name="ExpiresAt">Data-limite opcional, posterior ao instante do pedido.</param>
public sealed record JobRequest(
    string? Title,
    string? Description,
    decimal? MinSalary,
    decimal? MaxSalary,
    Guid LocationId,
    ContractType? ContractType,
    WorkMode? WorkMode,
    List<Guid>? SkillIds,
    List<Guid>? BenefitIds,
    bool? IsUrgent,
    DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty.
    /// </summary>
    public static JobRequest Empty { get; } = new(null, null, null, null, Guid.Empty, null, null, null, null, null, null);
}
