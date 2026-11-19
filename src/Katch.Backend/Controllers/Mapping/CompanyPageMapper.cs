// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Files;

namespace Katch.Backend.Controllers.Mapping;

/// <summary>
/// Converte a <see cref="Company"/> na <see cref="CompanyPageDto"/>, a mesma para o Recrutador (6.4.5, 6.4.9) e para o
/// Candidato (6.3.3). Os caminhos guardados passam a URL da API; o caminho no sistema de ficheiros nunca é enviado ao
/// cliente (documentação da API v02, secção 2.6). Passou da camada de serviços para a camada da API (I138): os serviços
/// devolvem a Empresa e o controller cria a página.
/// </summary>
public static class CompanyPageMapper
{
    /// <summary>
    /// Cria a página de apresentação da Empresa, sem os contactos (RF017, RF025).
    /// </summary>
    /// <param name="company">Empresa devolvida pelo serviço.</param>
    /// <param name="fileUrlBuilder">Conversor dos caminhos guardados nos URL da API.</param>
    /// <returns>A página de apresentação.</returns>
    public static CompanyPageDto ToPageDto(this Company company, IFileUrlBuilder fileUrlBuilder)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(fileUrlBuilder);

        return new CompanyPageDto(
            company.Id,
            company.CompanyName,
            company.Description,
            company.Website,
            fileUrlBuilder.ToUrl(company.LogoPath),
            fileUrlBuilder.ToUrls(company.PhotoPaths));
    }
}
