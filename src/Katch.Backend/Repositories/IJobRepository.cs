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
    /// Obtém uma vaga com as competências e os benefícios, acompanhada pelo contexto, para a alterar. As vagas eliminadas
    /// não são devolvidas (DM-09).
    /// </summary>
    /// <param name="id">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A vaga, ou <see langword="null"/> se não existir ou estiver eliminada.</returns>
    public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta uma vaga nova, gravada no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte (RF050).
    /// </summary>
    /// <param name="job">Vaga nova.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a vaga foi acrescentada.</returns>
    public Task AddAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as vagas da Empresa, só para leitura, com as competências e os benefícios, sem as eliminadas, da mais
    /// recente para a mais antiga (RF059, RF060). A ordem de criação é a do registo <c>JOB_CREATED</c>, porque a
    /// <c>job</c> não tem <c>created_at</c> (DM-22); as vagas sem esse registo (inseridas por SQL) ficam no fim.
    /// </summary>
    /// <param name="companyId">Identificador da Empresa.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As vagas da Empresa.</returns>
    public Task<List<Job>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se a vaga tem pelo menos um interesse registado, em qualquer estado da decisão (RF058; DM-21). As recusas
    /// do Candidato não contam.
    /// </summary>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><see langword="true"/> se houver interesses.</returns>
    public Task<bool> HasInterestsAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista, com acompanhamento de alterações, as vagas publicadas e não eliminadas cuja data-limite foi atingida no
    /// instante indicado (<c>expires_at &lt;= now</c>), para o encerramento automático (RF057; modelo de classes do
    /// backend v02, secção 4). A regra é a de <see cref="Job.IsExpired(DateTimeOffset)"/>.
    /// </summary>
    /// <param name="now">Instante da verificação, em UTC.</param>
    /// <param name="cancellationToken">Cancelamento da execução.</param>
    /// <returns>As vagas a encerrar.</returns>
    public Task<List<Job>> ListExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
