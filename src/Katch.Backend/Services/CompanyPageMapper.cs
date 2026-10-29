// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Files;

namespace Katch.Backend.Services;

/// <summary>
/// Converte a <see cref="Company"/> na <see cref="CompanyPageDto"/>, a mesma para o Recrutador (6.4.5, 6.4.9) e para o
/// Candidato (6.3.3). Os caminhos guardados passam a URL da API; o caminho no sistema de ficheiros nunca é enviado ao
/// cliente (documentação da API v02, secção 2.6).
/// </summary>
internal static class CompanyPageMapper
{
    /// <summary>
    /// Cria a página de apresentação da Empresa.
    /// </summary>
    /// <param name="company">Empresa.</param>
    /// <param name="fileUrlBuilder">Conversor dos caminhos guardados nos URL da API.</param>
    /// <returns>A página, com a galeria pela ordem de apresentação.</returns>
    public static CompanyPageDto ToPage(Company company, IFileUrlBuilder fileUrlBuilder)
    {
        return new CompanyPageDto(
            company.Id,
            company.CompanyName,
            company.Description,
            company.Website,
            fileUrlBuilder.ToUrl(company.LogoPath),
            fileUrlBuilder.ToUrls(company.PhotoPaths));
    }
}
