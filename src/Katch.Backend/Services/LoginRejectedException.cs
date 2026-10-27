// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// O início de sessão foi recusado. É convertida na resposta <c>401</c> ou <c>403</c> do catálogo de erros (documentação
/// da API v02, secções 4.3 e 6.1.3). A mensagem nunca inclui o correio nem a palavra-passe (RNF017).
/// </summary>
public sealed class LoginRejectedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoginRejectedException"/> class.
    /// </summary>
    /// <param name="reason">Motivo da recusa.</param>
    public LoginRejectedException(LoginRejection reason)
        : base("O início de sessão foi recusado.")
    {
        this.Reason = reason;
    }

    /// <summary>
    /// Gets the reason why the sign-in was refused.
    /// </summary>
    public LoginRejection Reason { get; }
}
