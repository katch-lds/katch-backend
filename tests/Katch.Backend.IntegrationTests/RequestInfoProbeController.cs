// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Endpoint de teste que devolve o IP do cliente e o esquema vistos pela API, depois dos cabeçalhos do proxy.
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
}
