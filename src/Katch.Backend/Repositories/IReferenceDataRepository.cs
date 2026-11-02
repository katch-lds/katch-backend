// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Acesso às listas pré-definidas (modelo de classes do backend v02, secção 4). As operações dos benefícios e da
/// manutenção das listas são acrescentadas pelas Issues que as usam.
/// </summary>
public interface IReferenceDataRepository
{
    /// <summary>
    /// Lista as localidades da lista pré-definida, só para leitura (DA, F002).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As localidades, sem ordem garantida.</returns>
    public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se a localidade existe na lista pré-definida (RF003, RF041, RF050).
    /// </summary>
    /// <param name="id">Identificador da localidade.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><see langword="true"/> se a localidade existir.</returns>
    public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma localidade da lista pré-definida, só para leitura, para apresentar a designação no perfil.
    /// </summary>
    /// <param name="id">Identificador da localidade.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A localidade, ou <see langword="null"/> se não existir.</returns>
    public Task<Location?> GetLocationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as competências da lista pré-definida, só para leitura (RF008, RF050).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As competências, sem ordem garantida.</returns>
    public Task<List<Skill>> ListSkillsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma competência da lista pré-definida, acompanhada pelo contexto, para a associar a um perfil (RF008).
    /// </summary>
    /// <param name="id">Identificador da competência.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A competência, ou <see langword="null"/> se não existir na lista.</returns>
    public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default);
}
