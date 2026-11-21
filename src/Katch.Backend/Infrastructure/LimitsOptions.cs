// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Limites globais da API: o ficheiro de configuração do repositório para os valores partilhados ou de alteração
/// frequente. Lê a secção <c>Limits</c> do <c>appsettings.json</c>; cada valor pode ser substituído pela variável de
/// ambiente <c>Limits__&lt;Nome&gt;</c> (por exemplo <c>Limits__MaxRequestBodyBytes</c>) e é validado no arranque por
/// <see cref="LimitsOptionsValidator"/>, que para a aplicação com uma mensagem clara se o valor for inválido.
/// </summary>
/// <remarks>
/// <para>Parâmetros: os valores da secção <c>Limits</c>, todos inteiros positivos.</para>
/// <para>Devolve: as opções validadas, por <c>IOptions&lt;LimitsOptions&gt;</c>.</para>
/// </remarks>
public sealed class LimitsOptions
{
    /// <summary>
    /// Nome da secção de configuração.
    /// </summary>
    public const string SectionName = "Limits";

    // ==================================================================================================================
    // 1. Pedidos
    // ==================================================================================================================

    /// <summary>
    /// Gets or sets the maximum size of a request body outside the uploads, in bytes (API v02, section 4.2: 64 KB).
    /// </summary>
    public long MaxRequestBodyBytes { get; set; } = 65_536;

    // ==================================================================================================================
    // 2. Limite de pedidos (janela fixa; por IP nos endpoints anónimos, por conta nos autenticados)
    // ==================================================================================================================

    /// <summary>
    /// Gets or sets the duration of the rate limit window, in seconds.
    /// </summary>
    public int RateLimitWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the number of sign-in requests per client IP in each window.
    /// </summary>
    public int LoginPermitLimit { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of registration requests (Candidate and Recruiter) per client IP in each window.
    /// </summary>
    public int RegistrationPermitLimit { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of requests to the anonymous list of locations per client IP in each window.
    /// </summary>
    public int LocationsPermitLimit { get; set; } = 30;

    /// <summary>
    /// Gets or sets the number of requests per authenticated account in each window (the 3-second polling uses about
    /// 40 per minute).
    /// </summary>
    public int UserPermitLimit { get; set; } = 300;
}
