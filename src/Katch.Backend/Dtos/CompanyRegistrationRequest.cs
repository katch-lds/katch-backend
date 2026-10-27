// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Dtos;

/// <summary>
/// Dados de registo da Empresa, usados no registo e na nova submissão do pedido recusado (documentação da API v02,
/// secções 6.4.1, 6.4.2 e 8.3; modelo de classes do backend v02, secção 7.1). Os campos são anuláveis para que um campo
/// em falta chegue ao serviço e seja indicado na mesma resposta que os restantes erros (RF042).
/// </summary>
/// <param name="CompanyName">Designação social, até 200 caracteres.</param>
/// <param name="TaxId">Número de identificação fiscal com exatamente 9 algarismos, único (P04).</param>
/// <param name="Industry">Setor de atividade da lista pré-definida (P15).</param>
/// <param name="Address">Morada, até 255 caracteres.</param>
/// <param name="LocationId">Identificador da localidade da lista pré-definida.</param>
/// <param name="ContactEmail">Correio eletrónico de contacto no formato local@domínio, até 255 caracteres (P04).</param>
/// <param name="ContactPhone">Telefone de contacto com exatamente 9 algarismos (P04).</param>
/// <param name="ResponsibleName">Nome do responsável da Empresa, até 150 caracteres.</param>
public sealed record CompanyRegistrationRequest(
    string? CompanyName,
    string? TaxId,
    Industry? Industry,
    string? Address,
    Guid LocationId,
    string? ContactEmail,
    string? ContactPhone,
    string? ResponsibleName)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty.
    /// </summary>
    public static CompanyRegistrationRequest Empty { get; } = new(null, null, null, null, Guid.Empty, null, null, null);
}
