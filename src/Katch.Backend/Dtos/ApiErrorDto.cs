// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Corpo de todas as respostas de erro da API (documentação da API v02, secção 4.1).
/// </summary>
/// <param name="Code">Código do catálogo de erros, por exemplo <c>ERR-01</c>.</param>
/// <param name="Message">Mensagem para o utilizador.</param>
/// <param name="Fields">Erros por campo; vazia quando não se aplica.</param>
/// <param name="BlockedUntil">Fim do bloqueio, quando o erro o indica; <see langword="null"/> nos restantes casos.</param>
public sealed record ApiErrorDto(
    string Code,
    string Message,
    IReadOnlyList<FieldErrorDto>? Fields = null,
    DateTimeOffset? BlockedUntil = null)
{
    /// <summary>
    /// Gets the errors per field; empty when it does not apply.
    /// </summary>
    public IReadOnlyList<FieldErrorDto> Fields { get; init; } = Fields ?? [];
}
