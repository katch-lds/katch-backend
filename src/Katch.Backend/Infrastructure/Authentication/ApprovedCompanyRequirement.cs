// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.AspNetCore.Authorization;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Exige que o utilizador seja um Recrutador cuja Empresa existe e está aprovada (documentação da API v02,
/// secção 3.4). É avaliado por <see cref="ApprovedCompanyAuthorizationHandler"/>.
/// </summary>
internal sealed class ApprovedCompanyRequirement : IAuthorizationRequirement
{
}
