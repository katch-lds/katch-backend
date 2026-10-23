// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Acesso aos Candidatos (<c>candidate</c>) (modelo de classes do backend v02, secção 4). As restantes operações do
/// modelo são acrescentadas pelas Issues do perfil, da quota e da administração.
/// </summary>
public interface ICandidateRepository
{
    /// <summary>
    /// Acrescenta um Candidato novo, gravado no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte.
    /// </summary>
    /// <param name="candidate">Candidato novo.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando o Candidato foi acrescentado.</returns>
    public Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default);
}
