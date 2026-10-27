// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// A recusa ou o interesse numa vaga foi rejeitado. É convertida na resposta do catálogo de erros que corresponde ao
/// <see cref="Reason"/> (documentação da API v02, secção 4.3: ERR-20, ERR-32 e ERR-33).
/// </summary>
public sealed class JobAnswerRejectedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobAnswerRejectedException"/> class.
    /// </summary>
    /// <param name="reason">Motivo da rejeição.</param>
    public JobAnswerRejectedException(JobAnswerRejection reason)
        : base($"A resposta à vaga foi rejeitada: {reason}.")
    {
        this.Reason = reason;
    }

    /// <summary>
    /// Gets the reason why the answer was rejected.
    /// </summary>
    public JobAnswerRejection Reason { get; }
}
