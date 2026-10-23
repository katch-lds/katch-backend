// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Nomes das políticas de autorização. «Qualquer conta» é a política por omissão (<c>[Authorize]</c>): utilizador
/// autenticado.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Só contas de Candidato.
    /// </summary>
    public const string Candidate = "Candidate";

    /// <summary>
    /// Só contas de Recrutador.
    /// </summary>
    public const string Recruiter = "Recruiter";

    /// <summary>
    /// Contas de Candidato ou de Recrutador; o Administrador é rejeitado.
    /// </summary>
    public const string CandidateOrRecruiter = "CandidateOrRecruiter";

    /// <summary>
    /// Só contas de Administrador.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Só Recrutadores cuja Empresa está aprovada.
    /// </summary>
    public const string ApprovedCompany = "ApprovedCompany";
}
