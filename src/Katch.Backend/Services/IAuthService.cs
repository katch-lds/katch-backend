// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;

namespace Katch.Backend.Services;

/// <summary>
/// Registo, criação de conta de Recrutador, início de sessão e alteração da palavra-passe (modelo de classes do
/// backend v02, secção 5.1).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Regista um Candidato: valida todos os campos no mesmo pedido, cria a conta ativa com o resumo da palavra-passe e
    /// a quota de 10 interesses, e inicia a sessão (documentação da API v02, secção 6.1.1; RF003 a RF005).
    /// </summary>
    /// <param name="request">Dados do registo.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A credencial de sessão da conta criada.</returns>
    /// <exception cref="ValidationFailedException">Se um ou mais campos não cumprirem a validação automática.</exception>
    public Task<LoginResponse> RegisterCandidateAsync(
        RegisterCandidateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria a conta de Recrutador: valida todos os campos no mesmo pedido, cria a conta ativa e sem Empresa com o resumo
    /// da palavra-passe e inicia a sessão (documentação da API v02, secção 6.1.2; RF037).
    /// </summary>
    /// <param name="request">Dados da conta.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A credencial de sessão da conta criada.</returns>
    /// <exception cref="ValidationFailedException">Se um ou mais campos não cumprirem a validação automática.</exception>
    public Task<LoginResponse> CreateRecruiterAccountAsync(
        CreateRecruiterAccountRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia a sessão. Verifica, por esta ordem, as credenciais, o estado da conta e o ponto de acesso, de modo que o
    /// estado da conta e o ponto de acesso só são revelados a quem apresenta a palavra-passe correta (documentação da
    /// API v02, secção 6.1.3; RF001, RF038, RF083, RNF009). A situação da Empresa do Recrutador não é consultada.
    /// </summary>
    /// <param name="request">Correio eletrónico e palavra-passe.</param>
    /// <param name="app">Ponto de acesso indicado pelo cliente.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A credencial de sessão da conta.</returns>
    /// <exception cref="ValidationFailedException">Se o correio ou a palavra-passe estiverem vazios.</exception>
    /// <exception cref="LoginRejectedException">
    /// Se as credenciais não coincidirem, a conta estiver bloqueada ou suspensa, ou o tipo de conta não puder entrar por
    /// este ponto de acesso.
    /// </exception>
    public Task<LoginResponse> LoginAsync(
        LoginRequest request,
        ClientApp app,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Altera a palavra-passe da própria conta (documentação da API v02, secção 6.1.5; RF002, RF040, RF084). A
    /// credencial de sessão atual mantém-se válida até expirar.
    /// </summary>
    /// <param name="userId">Identificador da conta, obtido da credencial de sessão.</param>
    /// <param name="request">Palavra-passe atual e palavra-passe nova.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a nova palavra-passe foi gravada.</returns>
    /// <exception cref="ValidationFailedException">
    /// Se a palavra-passe atual estiver errada ou a nova não cumprir o P05.
    /// </exception>
    public Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}
