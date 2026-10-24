// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Registo, criação de conta e início de sessão (modelo de classes do backend v02, secção 5.1). As operações
/// <c>LoginAsync</c> e <c>ChangePasswordAsync</c> são acrescentadas pela Issue I054.
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
}
