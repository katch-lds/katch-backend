// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Registo da Empresa e estado do pedido (modelo de classes do backend v02, secção 5.1). As restantes operações
/// (dados de registo, alteração, página, logótipo e galeria) são acrescentadas pelas Issues que as implementam.
/// </summary>
public interface ICompanyService
{
    /// <summary>
    /// Regista a Empresa do Recrutador: valida todos os campos no mesmo pedido, cria a Empresa no estado pendente e
    /// regista <c>COMPANY_SUBMITTED</c> no <c>operation_log</c> (documentação da API v02, secção 6.4.1; RF041 a RF043,
    /// RF112).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="request">Dados de registo da Empresa.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O estado do pedido, pendente.</returns>
    /// <exception cref="OperationConflictException">ERR-40, se o Recrutador já registou uma Empresa.</exception>
    /// <exception cref="ValidationFailedException">Se um ou mais campos não cumprirem a validação automática.</exception>
    public Task<CompanyStatusDto> RegisterAsync(
        Guid recruiterId,
        CompanyRegistrationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrige os dados do pedido recusado e submete-o novamente: a Empresa passa de recusada a pendente, o motivo é
    /// limpo, a data de submissão é atualizada e fica registado <c>COMPANY_SUBMITTED</c> (documentação da API v02,
    /// secção 6.4.2; RF045, RF112).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="request">Dados de registo corrigidos, completos.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O estado do pedido, pendente.</returns>
    /// <exception cref="ElementNotFoundException">ERR-20, se o Recrutador ainda não registou a Empresa.</exception>
    /// <exception cref="OperationConflictException">ERR-30, se a Empresa não estiver recusada.</exception>
    /// <exception cref="ValidationFailedException">Se um ou mais campos não cumprirem a validação automática.</exception>
    public Task<CompanyStatusDto> ResubmitAsync(
        Guid recruiterId,
        CompanyRegistrationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devolve o estado do pedido de registo, com o motivo da recusa só no estado recusado (documentação da API v02,
    /// secção 6.4.3; RF044, RF039).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O estado do pedido.</returns>
    /// <exception cref="ElementNotFoundException">ERR-20, se o Recrutador ainda não registou a Empresa.</exception>
    public Task<CompanyStatusDto> GetStatusAsync(Guid recruiterId, CancellationToken cancellationToken = default);
}
