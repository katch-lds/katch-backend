// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Errors;

namespace Katch.Backend.Services;

/// <summary>
/// O elemento pedido não existe ou não está disponível para o utilizador. É convertida na resposta <c>404</c> ERR-20
/// (documentação da API v02, secções 4.2 e 4.3).
/// </summary>
public sealed class ElementNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ElementNotFoundException"/> class.
    /// </summary>
    public ElementNotFoundException()
        : base(ApiErrorResponse.NotFoundMessage)
    {
    }
}
