// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// O ficheiro enviado não é aceite (formato, assinatura do conteúdo, dimensão ou ficheiro em falta). É convertida na
/// resposta <c>400</c> ERR-11, com o motivo no campo <c>file</c> (documentação da API v02, secção 4.3; RNF015, RNF016).
/// </summary>
public sealed class FileRejectedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileRejectedException"/> class.
    /// </summary>
    /// <param name="reason">Motivo da recusa, como aparece em <c>fields[0].reason</c>.</param>
    public FileRejectedException(string reason)
        : base("O ficheiro não é aceite.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        this.Reason = reason;
    }

    /// <summary>
    /// Gets the reason of the refusal.
    /// </summary>
    public string Reason { get; }
}
