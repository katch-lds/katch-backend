// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Katch.Backend.Infrastructure.Api;

/// <summary>
/// Marca no Swagger as operações que exigem a credencial de sessão. Como a política de reserva exige autenticação em
/// todos os endpoints, só as operações com <c>[AllowAnonymous]</c> ficam sem o requisito.
/// </summary>
internal sealed class BearerRequirementOperationFilter : IOperationFilter
{
    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        bool anonymous = context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (anonymous)
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SwaggerBearerAuthenticationExtensions.SchemeName, context.Document)] = [],
        });
    }
}
