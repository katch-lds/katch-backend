// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Dtos;
using Katch.Backend.Repositories;
using Katch.Backend.Services;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes da consulta das localidades em <see cref="ReferenceListService"/> (documentação da API v02, secção 6.13.1).
/// </summary>
public sealed class ReferenceListServiceTests
{
    /// <summary>
    /// As localidades vêm como «Localidade (Distrito)», por ordem alfabética portuguesa e sem coordenadas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LocationsAreNamedWithDistrictAndSortedAlphabetically()
    {
        var evora = new Location("Évora", "Évora", 38.571m, -7.909m);
        var guimaraes = new Location("Guimarães", "Braga", 41.444m, -8.296m);
        var braga = new Location("Braga", "Braga", 41.545m, -8.426m);
        var esposende = new Location("Esposende", "Braga", 41.532m, -8.781m);
        var service = new ReferenceListService(new Locations(guimaraes, evora, braga, esposende));

        List<ReferenceItemDto> items = await service.ListLocationsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new ReferenceItemDto(braga.Id, "Braga (Braga)"),
                new ReferenceItemDto(esposende.Id, "Esposende (Braga)"),
                new ReferenceItemDto(evora.Id, "Évora (Évora)"),
                new ReferenceItemDto(guimaraes.Id, "Guimarães (Braga)"),
            ],
            items);
    }

    /// <summary>
    /// Duas localidades com o mesmo nome distinguem-se pelo distrito (DAPI-08).
    /// </summary>
    [Fact]
    public void DisplayNameIncludesTheDistrict()
    {
        Assert.Equal("Almada (Setúbal)", ReferenceListService.DisplayName(new Location("Almada", "Setúbal", 38.68m, -9.16m)));
    }

    private sealed class Locations : IReferenceDataRepository
    {
        private readonly Location[] locations;

        public Locations(params Location[] locations)
        {
            this.locations = locations;
        }

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.locations.ToList());
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.locations.Any(location => location.Id == id));
        }

        public Task<Location?> GetLocationAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Benefit?> GetBenefitAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
