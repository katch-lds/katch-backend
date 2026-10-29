// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Perfil profissional do Candidato (modelo de classes do backend v02, secção 5.1): consulta e gravação do perfil, das
/// preferências de procura e das hiperligações, e envio e obtenção da fotografia e do curriculum vitae. As competências
/// são acrescentadas pela Issue da gestão de competências (I062).
/// </summary>
public interface ICandidateProfileService
{
    /// <summary>
    /// Consulta o perfil profissional do Candidato (RF006 a RF012).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O perfil, com o URL da fotografia e sem os caminhos dos ficheiros.</returns>
    /// <exception cref="ElementNotFoundException">Se o Candidato não existir.</exception>
    public Task<CandidateProfileDto> GetAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava a função pretendida, a localidade, a disponibilidade e o resumo da experiência (RF006; P08, P14). Os
    /// campos opcionais a <see langword="null"/> são limpos.
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="request">Novos valores dos quatro campos.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O perfil atualizado.</returns>
    /// <exception cref="ValidationFailedException">Se algum campo não cumprir a validação (todos são indicados).</exception>
    /// <exception cref="ElementNotFoundException">Se o Candidato não existir.</exception>
    public Task<CandidateProfileDto> UpdateAsync(
        Guid candidateId,
        UpdateCandidateProfileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava as preferências de procura, aplicadas ao cartão seguinte (RF007, RF023; P06, P09, P16). Um valor
    /// <see langword="null"/> ou uma lista vazia significa sem filtro nesse critério.
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="request">Novas preferências.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As preferências gravadas.</returns>
    /// <exception cref="ValidationFailedException">
    /// Se a distância, a pretensão salarial ou algum valor das listas não cumprir a validação; as preferências
    /// anteriores mantêm-se.
    /// </exception>
    /// <exception cref="ElementNotFoundException">Se o Candidato não existir.</exception>
    public Task<SearchPreferencesDto> UpdatePreferencesAsync(
        Guid candidateId,
        SearchPreferencesDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Substitui as hiperligações profissionais, de 0 a 3, pela ordem indicada (RF012; P10).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="request">Endereços, pela ordem de apresentação; a lista vazia apaga todas.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O perfil atualizado.</returns>
    /// <exception cref="ValidationFailedException">
    /// Se houver mais de 3 endereços, ou algum sem <c>http://</c> ou <c>https://</c>, acima de 500 caracteres ou repetido.
    /// </exception>
    /// <exception cref="ElementNotFoundException">Se o Candidato não existir.</exception>
    public Task<CandidateProfileDto> SetLinksAsync(
        Guid candidateId,
        SetLinksRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda a fotografia de perfil, substituindo e eliminando a anterior (RF010; RNF015, RNF016; P01).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="file">Ficheiro enviado no campo <c>file</c>; <see langword="null"/> se faltar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O URL da API que serve a fotografia.</returns>
    /// <exception cref="FileRejectedException">Se o ficheiro faltar, não for JPEG nem PNG ou exceder 5 MB.</exception>
    /// <exception cref="ElementNotFoundException">Se o Candidato não existir.</exception>
    public Task<string> UploadPhotoAsync(Guid candidateId, IFormFile? file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda o curriculum vitae, substituindo e eliminando o anterior (RF011; RNF007, RNF015, RNF016; P02).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="file">Ficheiro enviado no campo <c>file</c>; <see langword="null"/> se faltar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando o curriculum vitae foi guardado.</returns>
    /// <exception cref="FileRejectedException">Se o ficheiro faltar, não for PDF ou exceder 5 MB.</exception>
    /// <exception cref="ElementNotFoundException">Se o Candidato não existir.</exception>
    public Task UploadCvAsync(Guid candidateId, IFormFile? file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o curriculum vitae do próprio Candidato (RF011; RNF007).
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato da sessão.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O ficheiro PDF.</returns>
    /// <exception cref="ElementNotFoundException">Se o Candidato não tiver curriculum vitae.</exception>
    public Task<FileContent> GetCvAsync(Guid candidateId, CancellationToken cancellationToken = default);
}
