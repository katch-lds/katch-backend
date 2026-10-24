// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Valores da claim <c>role</c> da credencial, um por tipo de conta.
/// </summary>
public static class AccountRoles
{
    /// <summary>
    /// Conta de Candidato.
    /// </summary>
    public const string Candidate = "CANDIDATE";

    /// <summary>
    /// Conta de Recrutador.
    /// </summary>
    public const string Recruiter = "RECRUITER";

    /// <summary>
    /// Conta de Administrador.
    /// </summary>
    public const string Admin = "ADMIN";

    /// <summary>
    /// Traduz o tipo de conta no valor da claim <c>role</c>, sem depender do nome da enumeração.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <returns>O valor da claim <c>role</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Se o tipo de conta não for conhecido.</exception>
    public static string FromUserType(UserType userType)
    {
        return userType switch
        {
            UserType.Candidate => Candidate,
            UserType.Recruiter => Recruiter,
            UserType.Admin => Admin,
            _ => throw new ArgumentOutOfRangeException(nameof(userType), userType, "Tipo de conta desconhecido."),
        };
    }
}
