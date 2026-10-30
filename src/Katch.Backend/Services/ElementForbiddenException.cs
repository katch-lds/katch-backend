// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Errors;

namespace Katch.Backend.Services;

/// <summary>
/// O elemento pedido existe, mas é de outro utilizador ou de outra Empresa (RF059, RNF006). É convertida na resposta
/// <c>403</c> ERR-08 (documentação da API v02, secções 3.3 e 4.3; DAPI-04).
/// </summary>
public sealed class ElementForbiddenException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ElementForbiddenException"/> class.
    /// </summary>
    public ElementForbiddenException()
        : base(ApiErrorResponse.ElementForbiddenMessage)
    {
    }
}
