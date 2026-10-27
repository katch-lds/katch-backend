// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Um ou mais campos do pedido não cumprem a validação automática. É convertida na resposta <c>400</c> ERR-10, com
/// todos os campos em causa (documentação da API v02, secção 4.1; RF004, RNF008).
/// </summary>
public sealed class ValidationFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationFailedException"/> class.
    /// </summary>
    /// <param name="fields">Um erro por campo em causa; pelo menos um.</param>
    /// <exception cref="ArgumentException">Se a lista estiver vazia.</exception>
    public ValidationFailedException(IReadOnlyList<FieldErrorDto> fields)
        : base("Os dados enviados não cumprem a validação automática.")
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Count == 0)
        {
            throw new ArgumentException("Indique pelo menos um campo em erro.", nameof(fields));
        }

        this.Fields = fields;
    }

    /// <summary>
    /// Gets the errors per field.
    /// </summary>
    public IReadOnlyList<FieldErrorDto> Fields { get; }
}
