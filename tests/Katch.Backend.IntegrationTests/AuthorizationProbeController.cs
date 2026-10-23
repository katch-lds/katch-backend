// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Endpoints de teste, um por política de autorização, que só existem na assembly de testes.
/// </summary>
[ApiController]
[Route("test-authorization")]
public sealed class AuthorizationProbeController : ControllerBase
{
    /// <summary>
    /// Endpoint anónimo.
    /// </summary>
    /// <returns>200.</returns>
    [HttpGet("anonymous")]
    [AllowAnonymous]
    public IActionResult Anonymous()
    {
        return this.Ok();
    }

    /// <summary>
    /// Qualquer conta autenticada.
    /// </summary>
    /// <returns>200.</returns>
    [HttpGet("any")]
    [Authorize]
    public IActionResult Any()
    {
        return this.Ok();
    }

    /// <summary>
    /// Só Candidato.
    /// </summary>
    /// <returns>200.</returns>
    [HttpGet("candidate")]
    [Authorize(Policy = AuthorizationPolicies.Candidate)]
    public IActionResult Candidate()
    {
        return this.Ok();
    }

    /// <summary>
    /// Só Recrutador.
    /// </summary>
    /// <returns>200.</returns>
    [HttpGet("recruiter")]
    [Authorize(Policy = AuthorizationPolicies.Recruiter)]
    public IActionResult Recruiter()
    {
        return this.Ok();
    }

    /// <summary>
    /// Candidato ou Recrutador.
    /// </summary>
    /// <returns>200.</returns>
    [HttpGet("candidate-or-recruiter")]
    [Authorize(Policy = AuthorizationPolicies.CandidateOrRecruiter)]
    public IActionResult CandidateOrRecruiter()
    {
        return this.Ok();
    }

    /// <summary>
    /// Só Administrador.
    /// </summary>
    /// <returns>200.</returns>
    [HttpGet("admin")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public IActionResult Admin()
    {
        return this.Ok();
    }

    /// <summary>
    /// Só Recrutador com a Empresa aprovada.
    /// </summary>
    /// <returns>200.</returns>
    [HttpGet("approved-company")]
    [Authorize(Policy = AuthorizationPolicies.ApprovedCompany)]
    public IActionResult ApprovedCompany()
    {
        return this.Ok();
    }
}
