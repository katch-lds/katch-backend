// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Pedido de registo do Candidato (documentação da API v02, secções 6.1.1 e 8.1; modelo de classes do backend v02,
/// secção 7.1). Os textos são anuláveis para que um campo em falta chegue ao serviço e seja indicado na mesma resposta
/// que os restantes erros (RF004).
/// </summary>
/// <param name="FullName">Nome do Candidato, até 150 caracteres.</param>
/// <param name="Email">Correio eletrónico no formato local@domínio, até 255 caracteres e único.</param>
/// <param name="PhoneNumber">Contacto telefónico com exatamente 9 algarismos (P04).</param>
/// <param name="LocationId">Identificador da localidade da lista pré-definida.</param>
/// <param name="Password">Palavra-passe conforme o P05, até 128 caracteres.</param>
/// <param name="AcceptTerms">Aceitação das condições de utilização; tem de ser <see langword="true"/>.</param>
public sealed record RegisterCandidateRequest(
    string? FullName,
    string? Email,
    string? PhoneNumber,
    Guid LocationId,
    string? Password,
    bool AcceptTerms)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty.
    /// </summary>
    public static RegisterCandidateRequest Empty { get; } = new(null, null, null, Guid.Empty, null, false);
}
