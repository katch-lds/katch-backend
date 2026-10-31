// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Katch.Backend.Infrastructure.Api;

/// <summary>
/// Documenta no Swagger o corpo <c>multipart/form-data</c> com o campo <c>file</c> (binário) das operações de
/// carregamento de ficheiros, as marcadas com <see cref="UploadRequestFilterAttribute"/> (documentação da API v02,
/// secção 2.6). Os controllers lêem o campo <c>file</c> do pedido e não o recebem como parâmetro da ação, por isso o
/// Swagger UI não o conheceria sem este filtro e não permitiria escolher o ficheiro.
/// </summary>
internal sealed class FileUploadOperationFilter : IOperationFilter
{
    private const string FileField = "file";

    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        bool upload = context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<UploadRequestFilterAttribute>().Any();
        if (!upload)
        {
            return;
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            [FileField] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                        },
                        Required = new HashSet<string> { FileField },
                    },
                },
            },
        };
    }
}
