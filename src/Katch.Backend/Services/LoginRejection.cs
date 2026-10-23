// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// Motivo da recusa de um início de sessão, um por erro do catálogo da documentação da API v02 (secção 4.3).
/// </summary>
public enum LoginRejection
{
    /// <summary>
    /// Correio inexistente ou palavra-passe errada (ERR-02), sem distinguir os dois casos (RNF009).
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// Conta bloqueada (ERR-03).
    /// </summary>
    AccountBlocked,

    /// <summary>
    /// Conta suspensa (ERR-04).
    /// </summary>
    AccountSuspended,

    /// <summary>
    /// Tipo de conta que não pode entrar por este ponto de acesso (ERR-05).
    /// </summary>
    WrongAccessPoint,
}
