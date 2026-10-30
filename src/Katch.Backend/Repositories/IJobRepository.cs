// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Acesso às vagas (<c>job</c>) (modelo de classes do backend v02, secção 4). As restantes operações do modelo são
/// acrescentadas pelas Issues das vagas e da exploração.
/// </summary>
public interface IJobRepository
{
    /// <summary>
    /// Obtém uma vaga com o estado da sua Empresa. As vagas eliminadas não são devolvidas.
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A vaga e o estado da Empresa, ou <see langword="null"/> se a vaga não existir ou estiver eliminada.</returns>
    public Task<JobWithCompanyStatus?> GetWithCompanyStatusAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma vaga com as competências pretendidas, só para leitura, para confirmar a Empresa da vaga e identificar
    /// as competências do Candidato coincidentes (RF059, RF062). As vagas eliminadas não são devolvidas.
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A vaga, ou <see langword="null"/> se não existir ou estiver eliminada.</returns>
    public Task<Job?> GetWithSkillsAsync(Guid jobId, CancellationToken cancellationToken = default);
}
