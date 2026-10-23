// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Security.Claims;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Avalia o <see cref="ApprovedCompanyRequirement"/>: lê o estado da Empresa do Recrutador na base de dados em cada
/// pedido, sem cache, para que uma suspensão produza efeito no pedido seguinte. Quando a Empresa não está aprovada,
/// regista a mensagem do ERR-07 num <see cref="AuthorizationFailureReason"/>, que chega ao
/// <see cref="ApiErrorAuthorizationResultHandler"/> sem uma segunda consulta.
/// </summary>
internal sealed class ApprovedCompanyAuthorizationHandler : AuthorizationHandler<ApprovedCompanyRequirement>
{
    private readonly KatchDbContext database;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovedCompanyAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="database">Contexto da base de dados do pedido.</param>
    public ApprovedCompanyAuthorizationHandler(KatchDbContext database)
    {
        this.database = database;
    }

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApprovedCompanyRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Só se consulta a Empresa de um Recrutador: qualquer outro tipo de conta fica no ERR-06, sem ir à base.
        if (!context.User.IsInRole(AccountRoles.Recruiter)
            || !Guid.TryParse(context.User.FindFirstValue("sub"), out Guid recruiterId))
        {
            return;
        }

        CompanyStatus? status = await this.database.Companies
            .AsNoTracking()
            .Where(company => company.RecruiterId == recruiterId)
            .Select(company => (CompanyStatus?)company.Status)
            .SingleOrDefaultAsync();

        if (status == CompanyStatus.Approved)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail(new AuthorizationFailureReason(this, ApiErrorResponse.CompanyNotApprovedMessage(status)));
    }
}
