// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Única definição das Empresas visíveis aos Candidatos (RF014): aprovadas e com a conta do Recrutador ativa. A Empresa
/// tem um só Recrutador (<c>company.recruiter_id</c>), por isso, com a conta bloqueada, ninguém pode decidir sobre os
/// interesses nas suas vagas (FUN-006).
/// </summary>
internal static class CandidateVisibleCompanies
{
    /// <summary>
    /// Consulta das Empresas visíveis aos Candidatos, sem seguimento de alterações.
    /// </summary>
    /// <param name="context">Contexto da base de dados.</param>
    /// <returns>As Empresas aprovadas cujo Recrutador tem a conta ativa.</returns>
    internal static IQueryable<Company> CompaniesVisibleToCandidates(this KatchDbContext context)
    {
        return from company in context.Companies.AsNoTracking()
               join recruiter in context.Users.AsNoTracking() on company.RecruiterId equals recruiter.Id
               where company.Status == CompanyStatus.Approved && recruiter.Status == AccountStatus.Active
               select company;
    }
}
