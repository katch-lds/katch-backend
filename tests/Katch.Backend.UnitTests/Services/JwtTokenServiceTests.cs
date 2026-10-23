// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Security.Cryptography;
using System.Text;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes da emissão da credencial JWT, com um relógio controlado e um segredo gerado em cada execução.
/// </summary>
public sealed class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Issued = new(2026, 10, 23, 9, 30, 15, TimeSpan.Zero);

    private readonly string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>
    /// A claim <c>sub</c> é o identificador da conta, e o issuer e a audience são os valores por omissão.
    /// </summary>
    [Fact]
    public void TokenCarriesSubjectIssuerAndAudience()
    {
        AppUser user = NewUser(UserType.Candidate);

        JsonWebToken token = Read(this.CreateService(Issued).CreateToken(user));

        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal("katch-backend", token.Issuer);
        Assert.Equal(["katch-clients"], token.Audiences);
    }

    /// <summary>
    /// A claim <c>role</c> é explícita para cada tipo de conta.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="expectedRole">Valor esperado da claim <c>role</c>.</param>
    [Theory]
    [InlineData(UserType.Candidate, "CANDIDATE")]
    [InlineData(UserType.Recruiter, "RECRUITER")]
    [InlineData(UserType.Admin, "ADMIN")]
    public void RoleClaimMapsEachUserType(UserType userType, string expectedRole)
    {
        JsonWebToken token = Read(this.CreateService(Issued).CreateToken(NewUser(userType)));

        Assert.Equal(expectedRole, token.GetClaim("role").Value);
    }

    /// <summary>
    /// A credencial é assinada com HS256.
    /// </summary>
    [Fact]
    public void TokenIsSignedWithHs256()
    {
        JsonWebToken token = Read(this.CreateService(Issued).CreateToken(NewUser(UserType.Candidate)));

        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Alg);
    }

    /// <summary>
    /// A assinatura só é válida com o segredo configurado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SignatureIsValidOnlyWithTheConfiguredSecret()
    {
        string token = this.CreateService(Issued).CreateToken(NewUser(UserType.Candidate)).Token;
        var handler = new JsonWebTokenHandler();

        TokenValidationResult valid = await handler.ValidateTokenAsync(token, ValidationParameters(this.secret));
        TokenValidationResult other = await handler.ValidateTokenAsync(
            token,
            ValidationParameters(Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))));

        Assert.True(valid.IsValid);
        Assert.False(other.IsValid);
    }

    /// <summary>
    /// A claim <c>exp</c> é o instante de emissão mais 8 horas, e a <c>iat</c> é o instante de emissão.
    /// </summary>
    [Fact]
    public void ExpiryIsEightHoursAfterIssue()
    {
        JsonWebToken token = Read(this.CreateService(Issued).CreateToken(NewUser(UserType.Recruiter)));

        Assert.Equal(Issued.UtcDateTime, token.IssuedAt);
        Assert.Equal(Issued.AddHours(8).UtcDateTime, token.ValidTo);
    }

    /// <summary>
    /// O <c>ExpiresAt</c> da resposta coincide com a claim <c>exp</c>, mesmo que o relógio tenha frações de segundo.
    /// </summary>
    [Fact]
    public void ExpiresAtMatchesTheExpClaim()
    {
        DateTimeOffset now = Issued.AddMilliseconds(789);

        LoginResponse response = this.CreateService(now).CreateToken(NewUser(UserType.Admin));

        Assert.Equal(Issued.AddHours(8), response.ExpiresAt);
        Assert.Equal(Read(response).ValidTo, response.ExpiresAt.UtcDateTime);
    }

    /// <summary>
    /// A resposta traz a identificação e o tipo da conta.
    /// </summary>
    [Fact]
    public void ResponseCarriesUserIdAndType()
    {
        AppUser user = NewUser(UserType.Recruiter);

        LoginResponse response = this.CreateService(Issued).CreateToken(user);

        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(UserType.Recruiter, response.UserType);
    }

    /// <summary>
    /// Sem segredo, ou com um segredo curto, a configuração é recusada ao ser lida, sem mostrar o valor.
    /// </summary>
    /// <param name="configuredSecret">Segredo configurado, ou <see langword="null"/> se faltar.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curto")]
    public void InvalidSecretIsRejectedWithoutRevealingIt(string? configuredSecret)
    {
        using ServiceProvider provider = BuildProvider(configuredSecret);

        OptionsValidationException error = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);

        Assert.Contains("Jwt__Secret", error.Message, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(configuredSecret))
        {
            Assert.DoesNotContain(configuredSecret, error.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Um segredo com 32 bytes é aceite.
    /// </summary>
    [Fact]
    public void SecretWithThirtyTwoBytesIsAccepted()
    {
        using ServiceProvider provider = BuildProvider(new string('x', 32));

        JwtOptions options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;

        Assert.Equal("katch-backend", options.Issuer);
        Assert.Equal("katch-clients", options.Audience);
        Assert.Equal(8, options.LifetimeHours);
    }

    private static ServiceProvider BuildProvider(string? configuredSecret)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = configuredSecret })
                .Build());
        services.AddKatchJwtOptions();
        return services.BuildServiceProvider();
    }

    private static AppUser NewUser(UserType userType)
    {
        return new AppUser($"{Guid.NewGuid():N}@katch.pt", "hash", userType, Issued, Issued);
    }

    private static JsonWebToken Read(LoginResponse response)
    {
        return new JsonWebTokenHandler().ReadJsonWebToken(response.Token);
    }

    private static TokenValidationParameters ValidationParameters(string signingSecret)
    {
        return new TokenValidationParameters
        {
            ValidIssuer = "katch-backend",
            ValidAudience = "katch-clients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret)),
            ValidateLifetime = false,
        };
    }

    private JwtTokenService CreateService(DateTimeOffset now)
    {
        return new JwtTokenService(Options.Create(new JwtOptions { Secret = this.secret }), new FixedClock(now));
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now)
        {
            this.UtcNow = now;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
