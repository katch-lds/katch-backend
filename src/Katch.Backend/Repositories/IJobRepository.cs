// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

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
    /// Lista as vagas no estado publicada de todas as Empresas, com a designação da Empresa e o número de interesses
    /// (todos os estados da decisão), só para leitura (RF098).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As vagas publicadas com a contagem, sem ordem garantida.</returns>
    public Task<List<JobWithCount>> ListPublishedWithInterestCountAsync(CancellationToken cancellationToken = default);
}
