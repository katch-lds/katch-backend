// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Perfil profissional do Candidato (modelo de classes do backend v02, secção 5.1). Tem só as competências da lista e da
/// opção «Outro»; a consulta e a gravação do perfil, as preferências, as hiperligações, a fotografia e o curriculum vitae
/// são acrescentados pela Issue do perfil (I113).
/// </summary>
public interface ICandidateProfileService
{
    /// <summary>
    /// Associa ao perfil uma competência da lista pré-definida (RF008).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="request">Competência a acrescentar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O perfil atualizado.</returns>
    /// <exception cref="ValidationFailedException">
    /// Se <c>skillId</c> faltar, não pertencer à lista ou já estiver associada ao perfil.
    /// </exception>
    /// <exception cref="ElementNotFoundException">Se o Candidato não existir.</exception>
    public Task<CandidateProfileDto> AddSkillAsync(
        Guid candidateId,
        AddSkillRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retira do perfil uma competência da lista pré-definida (RF008).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="skillId">Identificador da competência associada ao perfil.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a competência foi retirada.</returns>
    /// <exception cref="ElementNotFoundException">Se a competência não estiver associada ao perfil.</exception>
    public Task RemoveSkillAsync(Guid candidateId, Guid skillId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta ao perfil uma competência da opção «Outro», validada no servidor segundo o P07 (RF009).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="request">Designação da competência.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O perfil atualizado.</returns>
    /// <exception cref="ValidationFailedException">
    /// Se <c>label</c> faltar, não cumprir o P07 ou já estiver indicada no perfil, sem distinção de maiúsculas.
    /// </exception>
    /// <exception cref="ElementNotFoundException">Se o Candidato não existir.</exception>
    public Task<CandidateProfileDto> AddCustomSkillAsync(
        Guid candidateId,
        AddCustomSkillRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retira do perfil uma competência da opção «Outro» (RF009).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="customSkillId">Identificador da competência «Outro».</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a competência foi retirada.</returns>
    /// <exception cref="ElementNotFoundException">Se a competência «Outro» não existir no perfil.</exception>
    public Task RemoveCustomSkillAsync(Guid candidateId, Guid customSkillId, CancellationToken cancellationToken = default);
}
