// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Katch.Backend.Infrastructure.Api;

/// <summary>
/// Documentação da autenticação no Swagger (documentação da API v02, secções 3.1 e 3.2): o botão «Authorize» da
/// interface em <c>/swagger</c> passa a enviar <c>Authorization: Bearer</c> nas operações que a exigem, e o cabeçalho
/// <c>X-Client-App</c> do início de sessão fica obrigatório.
/// </summary>
public static class SwaggerBearerAuthenticationExtensions
{
    /// <summary>
    /// Nome do esquema de segurança no documento OpenAPI.
    /// </summary>
    public const string SchemeName = "Bearer";

    /// <summary>
    /// Declara o esquema JWT Bearer, aplica-o às operações que exigem autenticação e documenta o cabeçalho
    /// <c>X-Client-App</c>.
    /// </summary>
    /// <param name="options">Opções da geração do Swagger.</param>
    /// <returns>As mesmas opções, para encadear chamadas.</returns>
    public static SwaggerGenOptions AddKatchBearerAuthentication(this SwaggerGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddSecurityDefinition(
            SchemeName,
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Credencial devolvida por POST /api/auth/login. Cole só o token, sem o prefixo «Bearer».",
            });
        options.OperationFilter<BearerRequirementOperationFilter>();
        options.OperationFilter<ClientAppHeaderOperationFilter>();
        return options;
    }
}
