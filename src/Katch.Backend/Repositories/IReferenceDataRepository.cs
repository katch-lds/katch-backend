// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Acesso às listas pré-definidas (modelo de classes do backend v02, secção 4). A consulta das competências é acrescentada
/// pela Issue da gestão das competências (I062).
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
    /// Lista os benefícios da lista pré-definida, só para leitura (RF050).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os benefícios, sem ordem garantida.</returns>
    public Task<List<Benefit>> ListBenefitsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma competência da lista pré-definida, acompanhada pelo contexto, para alterar a designação (RF100).
    /// </summary>
    /// <param name="id">Identificador da competência.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A competência, ou <see langword="null"/> se não existir na lista.</returns>
    public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém um benefício da lista pré-definida, acompanhado pelo contexto, para alterar a designação (RF102).
    /// </summary>
    /// <param name="id">Identificador do benefício.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O benefício, ou <see langword="null"/> se não existir na lista.</returns>
    public Task<Benefit?> GetBenefitAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se outra competência já tem a designação, sem distinção de maiúsculas (RF099, RF100; índice
    /// <c>uq_skill_name</c>).
    /// </summary>
    /// <param name="name">Designação, sem os espaços nas extremidades.</param>
    /// <param name="exceptId">Competência a ignorar (a própria, na alteração), ou <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><see langword="true"/> se outra competência tiver a mesma designação.</returns>
    public Task<bool> SkillNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se outro benefício já tem a designação, sem distinção de maiúsculas (RF101, RF102; índice
    /// <c>uq_benefit_name</c>).
    /// </summary>
    /// <param name="name">Designação, sem os espaços nas extremidades.</param>
    /// <param name="exceptId">Benefício a ignorar (o próprio, na alteração), ou <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><see langword="true"/> se outro benefício tiver a mesma designação.</returns>
    public Task<bool> BenefitNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta uma competência nova à lista, gravada no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte (RF099).
    /// </summary>
    /// <param name="skill">Competência nova.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a competência foi acrescentada.</returns>
    public Task AddSkillAsync(Skill skill, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta um benefício novo à lista, gravado no <see cref="IUnitOfWork.SaveChangesAsync"/> seguinte (RF101).
    /// </summary>
    /// <param name="benefit">Benefício novo.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando o benefício foi acrescentado.</returns>
    public Task AddBenefitAsync(Benefit benefit, CancellationToken cancellationToken = default);
}
