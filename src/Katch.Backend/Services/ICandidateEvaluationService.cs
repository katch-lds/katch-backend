// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Avaliação dos Candidatos interessados nas vagas da Empresa do Recrutador (modelo de classes do backend v02, secção
/// 5.1; UC11). Tem a lista de candidatos em espera, a abertura do perfil completo e a obtenção do curriculum vitae; a
/// aceitação e a recusa (UC12) são acrescentadas pela Issue que as implementa. Em todas as operações, a vaga do
/// interesse tem de pertencer à Empresa do Recrutador.
/// </summary>
public interface ICandidateEvaluationService
{
    /// <summary>
    /// Lista os Candidatos em espera de resposta numa vaga da Empresa, do interesse mais antigo para o mais recente
    /// (RF060, RF113). A vaga pode estar em qualquer estado não eliminado.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os Candidatos em espera; lista vazia se não houver.</returns>
    /// <exception cref="ElementNotFoundException">Se a vaga não existir ou estiver eliminada (ERR-20).</exception>
    /// <exception cref="ElementForbiddenException">Se a vaga for de outra Empresa (ERR-08).</exception>
    public Task<List<WaitingCandidateDto>> ListWaitingAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre o perfil completo do Candidato para a vaga do interesse, com as competências coincidentes com as pretendidas
    /// na vaga identificadas (RF061, RF062), e regista a primeira abertura, com a data e hora (RF118). As aberturas
    /// seguintes não alteram o registo.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="matchId">Identificador do interesse.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O perfil completo, com a data e hora da primeira abertura.</returns>
    /// <exception cref="ElementNotFoundException">
    /// Se o interesse não existir ou for a recusa da vaga pelo Candidato, que não é um interesse (ERR-20).
    /// </exception>
    /// <exception cref="ElementForbiddenException">Se a vaga do interesse for de outra Empresa (ERR-08).</exception>
    /// <exception cref="OperationConflictException">Se o interesse já não estiver em espera (ERR-35).</exception>
    public Task<CandidateFullProfileDto> OpenProfileAsync(Guid recruiterId, Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o curriculum vitae do Candidato, só enquanto o interesse está em espera numa vaga da Empresa (RF061,
    /// RNF007).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="matchId">Identificador do interesse.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O ficheiro PDF, que a resposta HTTP liberta.</returns>
    /// <exception cref="ElementNotFoundException">
    /// Se o interesse não existir, for a recusa da vaga pelo Candidato ou o Candidato não tiver curriculum vitae (ERR-20).
    /// </exception>
    /// <exception cref="ElementForbiddenException">Se a vaga do interesse for de outra Empresa (ERR-08).</exception>
    /// <exception cref="OperationConflictException">Se o interesse já não estiver em espera (ERR-35).</exception>
    public Task<FileContent> GetCvAsync(Guid recruiterId, Guid matchId, CancellationToken cancellationToken = default);
}
