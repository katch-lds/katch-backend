// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Configura a validação da credencial a partir de <see cref="JwtOptions"/>, já validadas no arranque: assinatura,
/// expiração (sem tolerância), issuer e audience.
/// </summary>
internal sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IOptions<JwtOptions> jwtOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureJwtBearerOptions"/> class.
    /// </summary>
    /// <param name="jwtOptions">Configuração JWT.</param>
    public ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
    {
        this.jwtOptions = jwtOptions;
    }

    /// <inheritdoc/>
    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        JwtOptions jwt = this.jwtOptions.Value;

        // As claims ficam com os nomes da credencial (sub, role), sem o mapeamento para os URIs do WS-Federation.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret!)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
    }

    /// <inheritdoc/>
    public void Configure(JwtBearerOptions options)
    {
        this.Configure(Options.DefaultName, options);
    }
}
