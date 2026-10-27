// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.Json;
using System.Text.Json.Serialization;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Katch.Backend.Infrastructure.Api;

/// <summary>
/// Convenções do JSON e das respostas de validação dos controllers (documentação da API v02, secções 2.3 e 4.1).
/// </summary>
public static class ApiConventionsMvcBuilderExtensions
{
    /// <summary>
    /// Enumerações em texto com maiúsculas e <c>_</c> (<c>"CANDIDATE"</c>, <c>"SERVICE_PROVISION"</c>), datas e horas em
    /// UTC com o sufixo <c>Z</c> (<c>"2026-10-09T14:30:00Z"</c>), o ERR-10 com
    /// todos os campos em causa para a <see cref="Services.ValidationFailedException"/> e para os pedidos que não podem
    /// ser lidos (por exemplo, um identificador que não é um <see cref="Guid"/>).
    /// </summary>
    /// <param name="builder">Construtor do MVC.</param>
    /// <returns>O mesmo construtor, para encadear chamadas.</returns>
    public static IMvcBuilder AddKatchApiConventions(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMvcOptions(options =>
        {
            options.Filters.Add<ValidationFailedExceptionFilter>();
            options.Filters.Add<ServiceExceptionFilter>();
            options.Filters.Add<LoginRejectedExceptionFilter>();
            options.Filters.Add<JobExplorationExceptionFilter>();
        });
        builder.AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        });
        builder.ConfigureApiBehaviorOptions(options =>
            options.InvalidModelStateResponseFactory = context =>
                new BadRequestObjectResult(ApiErrorResponse.ValidationFailed(ToFieldErrors(context.ModelState))));
        return builder;
    }

    /// <summary>
    /// Converte os erros de leitura do pedido em erros de campo: a chave <c>$.locationId</c> passa a <c>locationId</c>;
    /// os erros do corpo inteiro (JSON mal formado) não identificam um campo e ficam só no código ERR-10.
    /// </summary>
    /// <param name="modelState">Estado do modelo com os erros de leitura.</param>
    /// <returns>Um erro por campo identificado.</returns>
    internal static IReadOnlyList<FieldErrorDto> ToFieldErrors(ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        return modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 } && entry.Key.StartsWith("$.", StringComparison.Ordinal))
            .Select(entry => entry.Key[2..])
            .Distinct(StringComparer.Ordinal)
            .Select(field => new FieldErrorDto(field, ReasonFor(field)))
            .ToList();
    }

    private static string ReasonFor(string field)
    {
        return field switch
        {
            "locationId" or "industry" => FieldReasons.NotInList,
            "acceptTerms" => FieldReasons.AcceptTerms,
            _ => FieldReasons.InvalidValue,
        };
    }
}
