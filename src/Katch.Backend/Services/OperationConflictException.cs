// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Services.Models;

namespace Katch.Backend.Services;

/// <summary>
/// A operação não é possível no estado atual do elemento. É convertida na resposta <c>409</c> com o erro do catálogo
/// indicado (documentação da API v02, secções 4.2 e 4.3), por exemplo o ERR-30 (transição de estado inválida) ou o
/// ERR-40 (segundo registo de Empresa).
/// </summary>
public sealed class OperationConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationConflictException"/> class.
    /// </summary>
    /// <param name="error">Código e mensagem do erro do catálogo, criados por <see cref="ServiceErrors"/>.</param>
    public OperationConflictException(ServiceError error)
        : base(error?.Message)
    {
        ArgumentNullException.ThrowIfNull(error);

        this.Error = error;
    }

    /// <summary>
    /// Gets the catalog error converted into the body of the <c>409</c> response.
    /// </summary>
    public ServiceError Error { get; }
}
