// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Security.Claims;
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
    /// Recusa o pedido se a conta do <c>sub</c> não existir (401 ERR-01), estiver bloqueada (403 ERR-03) ou
    /// suspensa (403 ERR-04); os pedidos sem autenticação seguem para a autorização.
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

        if (!Guid.TryParse(context.User.FindFirstValue("sub"), out Guid userId))
        {
            await ApiErrorResponse.WriteInvalidSessionAsync(context);
            return;
        }

        var account = await database.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Status })
            .SingleOrDefaultAsync(context.RequestAborted);

        if (account is null)
        {
            await ApiErrorResponse.WriteInvalidSessionAsync(context);
            return;
        }

        switch (account.Status)
        {
            case AccountStatus.Blocked:
                await ApiErrorResponse.WriteAccountBlockedAsync(context);
                return;
            case AccountStatus.Suspended:
                await ApiErrorResponse.WriteAccountSuspendedAsync(context);
                return;
            default:
                await this.next(context);
                return;
        }
    }
}
