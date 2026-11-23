// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Verifica o estado da conta em cada pedido autenticado, antes da política de autorização, para que um bloqueio
/// produza efeito no pedido seguinte, sem esperar pela expiração da credencial.
/// </summary>
internal sealed class AccountStatusMiddleware
{
    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountStatusMiddleware"/> class.
    /// </summary>
    /// <param name="next">Passo seguinte do pipeline.</param>
    public AccountStatusMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    /// <summary>
    /// Recusa o pedido se a conta do <c>sub</c> não existir ou a sessão tiver terminado (versão da credencial diferente
    /// da da conta) com 401 ERR-01, ou se a conta estiver bloqueada (403 ERR-03) ou suspensa (403 ERR-04); os pedidos
    /// sem autenticação seguem para a autorização. A versão é lida na mesma consulta do estado.
    /// </summary>
    /// <param name="context">Contexto do pedido.</param>
    /// <param name="database">Contexto da base de dados do pedido.</param>
    /// <returns>Tarefa que termina quando o pedido foi tratado.</returns>
    public async Task InvokeAsync(HttpContext context, KatchDbContext database)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(database);

        // Os endpoints anónimos não dependem da conta: passam sem consultar a base de dados.
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || context.User.Identity?.IsAuthenticated != true)
        {
            await this.next(context);
            return;
        }

        if (!context.User.TryGetAccountId(out Guid userId) || !context.User.TryGetTokenVersion(out int tokenVersion))
        {
            await ApiErrorResponse.WriteInvalidSessionAsync(context);
            return;
        }

        var account = await database.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Status, user.TokenVersion })
            .SingleOrDefaultAsync(context.RequestAborted);

        if (account is null)
        {
            await ApiErrorResponse.WriteInvalidSessionAsync(context);
            return;
        }

        // O estado vem antes da versão: uma conta bloqueada ou suspensa recebe sempre ERR-03 ou ERR-04, mesmo com uma
        // credencial anterior ao bloqueio, que o bloqueio já revogou (SEG-018).
        switch (account.Status)
        {
            case AccountStatus.Blocked:
                await ApiErrorResponse.WriteAccountBlockedAsync(context);
                return;
            case AccountStatus.Suspended:
                await ApiErrorResponse.WriteAccountSuspendedAsync(context);
                return;
            default:
                if (account.TokenVersion != tokenVersion)
                {
                    await ApiErrorResponse.WriteInvalidSessionAsync(context);
                    return;
                }

                await this.next(context);
                return;
        }
    }
}
