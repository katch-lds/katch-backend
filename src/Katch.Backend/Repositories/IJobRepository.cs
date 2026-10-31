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
    /// Conta as vagas publicadas pela primeira vez no período, pela data da primeira publicação, que não muda numa nova
    /// publicação (RF096; modelo de dados v02, DM-10). As vagas eliminadas também contam, porque foram publicadas.
    /// </summary>
    /// <param name="periodStart">Início do período, inclusive.</param>
    /// <param name="periodEnd">Fim do período, exclusivo.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O número de vagas.</returns>
    public Task<int> CountFirstPublishedAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken = default);
}
