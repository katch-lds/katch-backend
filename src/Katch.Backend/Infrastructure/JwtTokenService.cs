// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text;
using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Emite credenciais JWT assinadas com HS256, com as claims <c>sub</c>, <c>role</c>, <c>iat</c> e <c>exp</c>.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JsonWebTokenHandler handler = new() { SetDefaultTimesOnTokenCreation = false };
    private readonly IOptions<JwtOptions> options;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtTokenService"/> class.
    /// </summary>
    /// <param name="options">Configuração JWT, validada no arranque.</param>
    /// <param name="clock">Relógio que dá o instante de emissão.</param>
    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        this.options = options;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public LoginResponse CreateToken(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        JwtOptions jwt = this.options.Value;

        // As claims iat e exp têm precisão de segundos; truncar aqui faz o ExpiresAt coincidir com a claim exp.
        DateTimeOffset issuedAt = DateTimeOffset.FromUnixTimeSeconds(this.clock.UtcNow.ToUnixTimeSeconds());
        DateTimeOffset expiresAt = issuedAt.AddHours(jwt.LifetimeHours);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                ["role"] = AccountRoles.FromUserType(user.UserType),
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret!)),
                SecurityAlgorithms.HmacSha256),
        };

        string token = this.handler.CreateToken(descriptor);
        return new LoginResponse(token, expiresAt, user.UserType, user.Id);
    }
}
