// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.AspNetCore.Diagnostics;

namespace Katch.Backend.Infrastructure.Errors;

/// <summary>
/// Passos do pipeline que dão o corpo <see cref="Dtos.ApiErrorDto"/> às respostas de erro que não passam pelos filtros
/// dos controllers: exceções não tratadas (ERR-50) e tipo de conteúdo errado (ERR-13).
/// </summary>
public static class ErrorPipelineExtensions
{
    /// <summary>
    /// Nome do endpoint que o encaminhamento escolhe quando nenhum endpoint aceita o <c>Content-Type</c> do pedido
    /// (<c>[Consumes]</c>). Não tem metadados de autorização, por isso a política de reserva respondia 401.
    /// </summary>
    internal const string UnsupportedMediaTypeEndpoint = "415 HTTP Unsupported Media Type";

    /// <summary>
    /// Responde às exceções não tratadas com o ERR-50 (500), sem pormenores da exceção, em qualquer ambiente. Um corpo
    /// que o servidor recusa ao ler (dimensão acima do limite do Kestrel) dá o ERR-12 (413); outro pedido que o servidor
    /// não consegue ler dá o ERR-10 (400). A exceção fica registada pelo <c>ExceptionHandlerMiddleware</c>.
    /// </summary>
    /// <param name="app">Construtor do pipeline.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static IApplicationBuilder UseKatchExceptionHandler(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = context => context.Features.Get<IExceptionHandlerFeature>()?.Error switch
            {
                BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                    ApiErrorResponse.WriteRequestTooLargeAsync(context),
                BadHttpRequestException => ApiErrorResponse.WriteValidationFailedAsync(context),
                _ => ApiErrorResponse.WriteUnexpectedErrorAsync(context),
            },
        });
    }

    /// <summary>
    /// Responde 415 ERR-13 a um pedido cujo <c>Content-Type</c> nenhum endpoint aceita, antes da autenticação, para que
    /// um endpoint anónimo não responda 401. Tem de ficar antes de <c>UseAuthentication</c>.
    /// </summary>
    /// <param name="app">Construtor do pipeline.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static IApplicationBuilder UseKatchUnsupportedMediaType(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use((context, next) =>
            context.GetEndpoint()?.DisplayName == UnsupportedMediaTypeEndpoint
                ? ApiErrorResponse.WriteUnsupportedContentTypeAsync(context)
                : next(context));
    }
}
