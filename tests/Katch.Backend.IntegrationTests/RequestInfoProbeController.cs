// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Endpoints de teste: o IP do cliente e o esquema vistos pela API, depois dos cabeçalhos do proxy, e uma falha
/// inesperada.
/// </summary>
[ApiController]
[Route("test-request-info")]
public sealed class RequestInfoProbeController : ControllerBase
{
    /// <summary>
    /// IP do cliente e esquema do pedido.
    /// </summary>
    /// <returns>200 com <c>remoteIp</c> e <c>scheme</c>.</returns>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        return this.Ok(new
        {
            RemoteIp = this.HttpContext.Connection.RemoteIpAddress?.ToString(),
            this.Request.Scheme,
        });
    }

    /// <summary>
    /// Lança uma exceção não tratada, com um texto que não pode chegar ao cliente.
    /// </summary>
    /// <returns>Nunca devolve.</returns>
    /// <exception cref="InvalidOperationException">Sempre.</exception>
    [HttpGet("failure")]
    [AllowAnonymous]
    public IActionResult Failure()
    {
        throw new InvalidOperationException($"Pormenor interno de {this.Request.Path}: Host=db;Password=segredo");
    }
}
