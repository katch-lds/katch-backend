// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain;

/// <summary>
/// Localidade da lista pré-definida (tabela <c>location</c>). É carregada pelos dados iniciais e não é editável
/// na plataforma (DA, F002); a aplicação só a lê e, por isso, a entidade não tem operações de escrita.
/// </summary>
public sealed class Location
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Location"/> class. Usado pelos dados iniciais.
    /// </summary>
    /// <param name="name">Nome da localidade, não vazio e com no máximo 100 caracteres.</param>
    /// <param name="district">Distrito, não vazio e com no máximo 50 caracteres; distingue localidades com o mesmo nome.</param>
    /// <param name="latitude">Latitude, de -90 a 90.</param>
    /// <param name="longitude">Longitude, de -180 a 180.</param>
    /// <exception cref="ArgumentException">Se o nome ou o distrito estiverem vazios ou forem demasiado longos.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Se a latitude ou a longitude estiverem fora do intervalo.</exception>
    public Location(string name, string district, decimal latitude, decimal longitude)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(district);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, 100);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(district.Length, 50);
        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, -90m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, 90m);
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180m);

        this.Id = Guid.NewGuid();
        this.Name = name;
        this.District = district;
        this.Latitude = latitude;
        this.Longitude = longitude;
    }

    private Location()
    {
        this.Name = string.Empty;
        this.District = string.Empty;
    }

    /// <summary>
    /// Gets the identifier of the location (<c>id</c>).
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the name of the location (<c>name</c>), unique together with the district.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the district (<c>district</c>), which distinguishes locations with the same name.
    /// </summary>
    public string District { get; private set; }

    /// <summary>
    /// Gets the latitude (<c>latitude</c>), from -90 to 90, used to compute the distance (RF015).
    /// </summary>
    public decimal Latitude { get; private set; }

    /// <summary>
    /// Gets the longitude (<c>longitude</c>), from -180 to 180, used to compute the distance (RF015).
    /// </summary>
    public decimal Longitude { get; private set; }
}
