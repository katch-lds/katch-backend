// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Dá o corpo <see cref="Dtos.ApiErrorDto"/> a todas as recusas da autorização: 401 ERR-01 quando não há sessão
/// válida e 403 ERR-06 quando a política não é cumprida. Cobre a credencial em falta, mal formada, com assinatura
/// inválida ou expirada, porque nestes casos o pedido chega aqui sem utilizador autenticado.
/// </summary>
/// <remarks>
/// Para acrescentar o ERR-07 (Empresa não aprovada), distinguir aqui a falha da política <c>ApprovedCompany</c>
/// em <see cref="PolicyAuthorizationResult.AuthorizationFailure"/> antes de escrever o ERR-06.
/// </remarks>
internal sealed class ApiErrorAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    /// <inheritdoc/>
    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Challenged)
        {
            return ApiErrorResponse.WriteInvalidSessionAsync(context);
        }

        if (authorizeResult.Forbidden)
        {
            return ApiErrorResponse.WriteAccountTypeNotAllowedAsync(context);
        }

        return next(context);
    }
}
