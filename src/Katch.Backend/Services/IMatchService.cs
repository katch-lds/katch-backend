// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Consulta dos matches confirmados, pelo Candidato e pelo Recrutador (documentação da API v02, secção 6.7.1; modelo de
/// classes do backend v02, secção 5; RF024, RF025, RF067).
/// </summary>
public interface IMatchService
{
    /// <summary>
    /// Lista os matches do Candidato, do mais recente para o mais antigo, incluindo os de vagas encerradas e de Empresas
    /// suspensas (RF024). Os contactos são os de registo da Empresa (RF025).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato, da credencial de sessão.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os matches; lista vazia se não houver nenhum.</returns>
    public Task<List<MatchDto>> ListForCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista os matches das vagas da Empresa do Recrutador, do mais recente para o mais antigo, com os contactos do
    /// Candidato (RF067).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador, da credencial de sessão.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os matches; lista vazia se não houver nenhum ou se o Recrutador não tiver Empresa.</returns>
    public Task<List<MatchDto>> ListForRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default);
}
