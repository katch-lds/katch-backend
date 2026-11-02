// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Avaliação dos Candidatos interessados nas vagas da Empresa do Recrutador (modelo de classes do backend v02, secção
/// 5.1; UC11 e UC12). Tem a lista de candidatos em espera, a abertura do perfil completo, a obtenção do curriculum vitae
/// e a decisão do Recrutador (aceitar ou recusar). Em todas as operações, a vaga do interesse tem de pertencer à Empresa
/// do Recrutador.
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

    /// <summary>
    /// Aceita o Candidato (RF063, RF065, RF066, RF068, RF117; T5): confirma o match com o Recrutador e a data da
    /// decisão e abre a conversa, numa única transação. Exige a abertura do perfil registada para a vaga.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="matchId">Identificador do interesse.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O match confirmado, com o contacto do Candidato.</returns>
    /// <exception cref="ElementNotFoundException">Se o interesse não existir ou for a recusa da vaga pelo Candidato (ERR-20).</exception>
    /// <exception cref="ElementForbiddenException">Se a vaga do interesse for de outra Empresa (ERR-08).</exception>
    /// <exception cref="OperationConflictException">
    /// Se o interesse já não estiver em espera, incluindo a perda na concorrência com outro pedido de decisão (ERR-35), ou
    /// se o perfil ainda não tiver sido aberto (ERR-34). Em qualquer erro, nada é alterado.
    /// </exception>
    public Task<MatchDto> AcceptAsync(Guid recruiterId, Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recusa o Candidato (RF064, RF065, RF117; T6): o interesse passa a recusado pelo Recrutador, com o autor e a data da
    /// decisão, e o Candidato deixa de ser apresentado para essa vaga. Não há conversa nem contacto. Exige a abertura do
    /// perfil registada para a vaga.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="matchId">Identificador do interesse.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a decisão foi gravada.</returns>
    /// <exception cref="ElementNotFoundException">Se o interesse não existir ou for a recusa da vaga pelo Candidato (ERR-20).</exception>
    /// <exception cref="ElementForbiddenException">Se a vaga do interesse for de outra Empresa (ERR-08).</exception>
    /// <exception cref="OperationConflictException">
    /// Se o interesse já não estiver em espera, incluindo a perda na concorrência com outro pedido de decisão (ERR-35), ou
    /// se o perfil ainda não tiver sido aberto (ERR-34). Em qualquer erro, nada é alterado.
    /// </exception>
    public Task RejectAsync(Guid recruiterId, Guid matchId, CancellationToken cancellationToken = default);
}
