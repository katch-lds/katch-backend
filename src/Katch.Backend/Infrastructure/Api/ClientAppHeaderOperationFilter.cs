// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.Json.Nodes;
using Katch.Backend.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Katch.Backend.Infrastructure.Api;

/// <summary>
/// Documenta no Swagger que o cabeçalho <c>X-Client-App</c> do início de sessão é obrigatório e só aceita <c>mobile</c> ou
/// <c>web</c> (documentação da API v02, secção 3.2). O parâmetro é anulável no controller para que a falta do cabeçalho
/// seja recusada com o ERR-10 e o campo identificado, e não por uma resposta genérica do framework.
/// </summary>
internal sealed class ClientAppHeaderOperationFilter : IOperationFilter
{
    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Parameters is null)
        {
            return;
        }

        foreach (OpenApiParameter parameter in operation.Parameters.OfType<OpenApiParameter>())
        {
            if (parameter.In == ParameterLocation.Header
                && string.Equals(parameter.Name, AuthController.ClientAppHeader, StringComparison.OrdinalIgnoreCase))
            {
                parameter.Required = true;
                parameter.Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [JsonValue.Create("mobile"), JsonValue.Create("web")],
                };
            }
        }
    }
}
