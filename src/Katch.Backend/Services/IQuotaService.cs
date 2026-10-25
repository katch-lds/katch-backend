// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// Reposição da quota de interesses (modelo de classes do backend v02, secção 5.1). A consulta da quota e a
/// manifestação de interesse aplicam a mesma regra no próprio pedido (<c>Candidate.RestoreQuotaIfDue</c>); este serviço
/// só repõe as quotas que ninguém consultou, por iniciativa da tarefa periódica.
/// </summary>
public interface IQuotaService
{
    /// <summary>
    /// Repõe na totalidade a quota dos Candidatos cujo período de bloqueio terminou (RF021, P11).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O número de quotas repostas.</returns>
    public Task<int> RestoreDueQuotasAsync(CancellationToken cancellationToken = default);
}
