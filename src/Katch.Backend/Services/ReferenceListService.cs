// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IReferenceListService"/> (modelo de classes do backend v02, secções 5.1 e 5.3).
/// </summary>
public sealed class ReferenceListService : IReferenceListService
{
    // Ordem alfabética portuguesa, igual em qualquer servidor: «Évora» fica junto de «Esposende» e não no fim.
    private static readonly StringComparer Alphabetical = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true);

    private readonly IReferenceDataRepository referenceData;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceListService"/> class.
    /// </summary>
    /// <param name="referenceData">Repositório das listas pré-definidas.</param>
    public ReferenceListService(IReferenceDataRepository referenceData)
    {
        this.referenceData = referenceData;
    }

    /// <summary>
    /// Designação da localidade, com o distrito para distinguir localidades com o mesmo nome (DAPI-08).
    /// </summary>
    /// <param name="location">Localidade.</param>
    /// <returns>A designação «Localidade (Distrito)».</returns>
    public static string DisplayName(Location location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return $"{location.Name} ({location.District})";
    }

    /// <inheritdoc/>
    public async Task<List<ReferenceItemDto>> ListLocationsAsync(CancellationToken cancellationToken = default)
    {
        List<Location> locations = await this.referenceData.ListLocationsAsync(cancellationToken);

        return locations
            .Select(location => new ReferenceItemDto(location.Id, DisplayName(location)))
            .OrderBy(item => item.Name, Alphabetical)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<List<ReferenceItemDto>> ListSkillsAsync(CancellationToken cancellationToken = default)
    {
        List<Skill> skills = await this.referenceData.ListSkillsAsync(cancellationToken);
        return skills
            .Select(skill => new ReferenceItemDto(skill.Id, skill.Name))
            .OrderBy(item => item.Name, Alphabetical)
            .ToList();
    }
}
