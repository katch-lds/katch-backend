// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Controllers;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Katch.Backend.Infrastructure.Cors;

/// <summary>
/// Registo na injeção de dependências e no pipeline da política CORS do frontend web (I130).
/// </summary>
public static class KatchCorsServiceCollectionExtensions
{
    /// <summary>
    /// Nome da política CORS da API.
    /// </summary>
    public const string PolicyName = "KatchWeb";

    /// <summary>
    /// Métodos HTTP usados pela API v02 (secção 2.5).
    /// </summary>
    private static readonly string[] AllowedMethods = [HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete];

    /// <summary>
    /// Cabeçalhos que o frontend web envia: a credencial, o tipo do corpo e o ponto de acesso do início de sessão.
    /// </summary>
    private static readonly string[] AllowedHeaders = [HeaderNames.Authorization, HeaderNames.ContentType, AuthController.ClientAppHeader];

    /// <summary>
    /// Cabeçalhos de resposta que o frontend web lê: o <c>Location</c> dos carregamentos de ficheiros (API v02, secção 2.6),
    /// que o browser esconderia num pedido de outra origem.
    /// </summary>
    private static readonly string[] ExposedHeaders = [HeaderNames.Location];

    /// <summary>
    /// Regista a configuração das origens (validada no arranque) e a política CORS: só as origens configuradas, os
    /// métodos e os cabeçalhos da API e nenhuma credencial por cookie, porque a sessão usa o token no cabeçalho.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <returns>A mesma coleção, para encadear chamadas.</returns>
    public static IServiceCollection AddKatchCors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidateOptions<KatchCorsOptions>, KatchCorsOptionsValidator>();
        services.AddOptions<KatchCorsOptions>().BindConfiguration(KatchCorsOptions.SectionName).ValidateOnStart();
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<KatchCorsOptions>>((cors, katch) =>
            cors.AddPolicy(PolicyName, policy => policy
                .WithOrigins([.. katch.Value.ParseOrigins().Select(NormalizeOrigin)])
                .WithMethods(AllowedMethods)
                .WithHeaders(AllowedHeaders)
                .WithExposedHeaders(ExposedHeaders)
                .DisallowCredentials()));
        return services;
    }

    /// <summary>
    /// Acrescenta a política CORS ao pipeline e responde aos pedidos de preflight (<c>OPTIONS</c>). Tem de ficar antes de
    /// <c>UseAuthentication</c>, para que o preflight não exija credencial e as respostas de erro 401 e 403 também
    /// tragam os cabeçalhos CORS.
    /// </summary>
    /// <param name="app">Construtor do pipeline.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static IApplicationBuilder UseKatchCors(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseCors(PolicyName);
    }

    /// <summary>
    /// Reduz uma origem válida à forma que o browser envia no cabeçalho <c>Origin</c>: esquema e anfitrião em
    /// minúsculas, porto só quando não é o do esquema e sem barra final.
    /// </summary>
    /// <param name="origin">Origem validada por <see cref="KatchCorsOptionsValidator"/>.</param>
    /// <returns>A origem normalizada.</returns>
    private static string NormalizeOrigin(string origin) => new Uri(origin).GetLeftPart(UriPartial.Authority);
}
