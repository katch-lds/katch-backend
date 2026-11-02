// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Resultado da validação de um ficheiro enviado.
/// </summary>
/// <param name="IsValid">Indica se o ficheiro é aceite.</param>
/// <param name="Reason">Motivo da recusa (<c>fields[].reason</c> do ERR-11); <see langword="null"/> se for aceite.</param>
public sealed record ValidationResult(bool IsValid, string? Reason)
{
    /// <summary>
    /// Gets the result of an accepted file.
    /// </summary>
    public static ValidationResult Valid { get; } = new(true, null);

    /// <summary>
    /// Cria o resultado de um ficheiro recusado.
    /// </summary>
    /// <param name="reason">Motivo da recusa.</param>
    /// <returns>O resultado.</returns>
    public static ValidationResult Invalid(string reason)
    {
        return new ValidationResult(false, reason);
    }
}
