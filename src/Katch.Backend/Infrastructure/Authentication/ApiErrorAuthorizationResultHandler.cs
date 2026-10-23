// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Dá o corpo <see cref="Dtos.ApiErrorDto"/> a todas as recusas da autorização: 401 ERR-01 quando não há sessão
/// válida, 403 ERR-07 quando o falhanço vem do requisito da Empresa aprovada (a mensagem, com o estado da Empresa,
/// já vem no motivo da falha) e 403 ERR-06 quando outra política não é cumprida. Cobre a credencial em falta, mal
/// formada, com assinatura inválida ou expirada, porque nestes casos o pedido chega aqui sem utilizador autenticado.
/// </summary>
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
            AuthorizationFailureReason? companyReason = authorizeResult.AuthorizationFailure?.FailureReasons
                .FirstOrDefault(reason => reason.Handler is ApprovedCompanyAuthorizationHandler);
            if (companyReason is not null)
            {
                return ApiErrorResponse.WriteCompanyNotApprovedAsync(context, companyReason.Message);
            }

            return ApiErrorResponse.WriteAccountTypeNotAllowedAsync(context);
        }

        return next(context);
    }
}
