// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Configuração da credencial JWT, lida da secção <c>Jwt</c>. Só o segredo é configurável: o emissor, o destinatário
/// e a validade são constantes. O segredo vem da variável de ambiente
/// <c>Jwt__Secret</c> e nunca do repositório.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// Nome da secção de configuração.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Número mínimo de bytes (em UTF-8) do segredo, o tamanho da chave do HS256.
    /// </summary>
    public const int MinSecretBytes = 32;

    /// <summary>
    /// Emissor (<c>iss</c>) da credencial, fixo pela API v02 (secção 3.1).
    /// </summary>
    public const string Issuer = "katch-backend";

    /// <summary>
    /// Destinatário (<c>aud</c>) da credencial, fixo pela API v02 (secção 3.1).
    /// </summary>
    public const string Audience = "katch-clients";

    /// <summary>
    /// Validade da credencial, em horas, a contar do instante de emissão; fixa pela API v02 (secção 3.1).
    /// </summary>
    public const int LifetimeHours = 8;

    /// <summary>
    /// Gets or sets the secret that signs the credential, with at least <see cref="MinSecretBytes"/> bytes.
    /// </summary>
    public string? Secret { get; set; }
}
