// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Configuração da credencial JWT, lida da secção <c>Jwt</c>. O segredo vem da variável de ambiente
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
    /// Gets or sets the issuer (<c>iss</c>) of the credential.
    /// </summary>
    public string Issuer { get; set; } = "katch-backend";

    /// <summary>
    /// Gets or sets the audience (<c>aud</c>) of the credential.
    /// </summary>
    public string Audience { get; set; } = "katch-clients";

    /// <summary>
    /// Gets or sets the validity, in hours, counted from the instant of issue.
    /// </summary>
    public int LifetimeHours { get; set; } = 8;

    /// <summary>
    /// Gets or sets the secret that signs the credential, with at least <see cref="MinSecretBytes"/> bytes.
    /// </summary>
    public string? Secret { get; set; }
}
