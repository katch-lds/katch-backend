// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Registo da Empresa, estado do pedido, dados de registo e página de apresentação (logótipo e galeria) da Empresa do
/// Recrutador (modelo de classes do backend v02, secção 5.1).
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

    /// <summary>
    /// Devolve os dados de registo da Empresa do Recrutador, em qualquer estado, para pré-preencher os formulários de
    /// correção e de alteração (documentação da API v02, secção 6.4.8; RF044, RF045, RF049).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os oito dados de registo, com a designação da localidade.</returns>
    /// <exception cref="ElementNotFoundException">ERR-20, se o Recrutador ainda não registou a Empresa.</exception>
    public Task<CompanyRegistrationDto> GetRegistrationAsync(Guid recruiterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Altera os dados de registo da Empresa aprovada, que se mantém aprovada, e regista <c>COMPANY_UPDATED</c> no
    /// <c>operation_log</c> (documentação da API v02, secção 6.4.4; RF049, RF112). O NIF tem de ser igual ao registado.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão, com a Empresa aprovada.</param>
    /// <param name="request">Dados de registo completos, com o mesmo NIF.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O estado do pedido, aprovado.</returns>
    /// <exception cref="ElementNotFoundException">ERR-20, se o Recrutador ainda não registou a Empresa.</exception>
    /// <exception cref="ValidationFailedException">
    /// Se um ou mais campos não cumprirem a validação automática, incluindo o NIF diferente do registado.
    /// </exception>
    public Task<CompanyStatusDto> UpdateRegistrationAsync(
        Guid recruiterId,
        CompanyRegistrationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devolve a página de apresentação da Empresa aprovada do Recrutador (documentação da API v02, secção 6.4.9;
    /// RF046 a RF048).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão, com a Empresa aprovada.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A página de apresentação.</returns>
    /// <exception cref="ElementNotFoundException">ERR-20, se o Recrutador ainda não registou a Empresa.</exception>
    public Task<CompanyPageDto> GetPageAsync(Guid recruiterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava a descrição da atividade e o sítio na Internet da página de apresentação (documentação da API v02, secção
    /// 6.4.5; RF046; P08, P10). Um valor nulo ou só com espaços limpa o campo.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão, com a Empresa aprovada.</param>
    /// <param name="request">Descrição e sítio na Internet.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A página de apresentação atualizada.</returns>
    /// <exception cref="ElementNotFoundException">ERR-20, se o Recrutador ainda não registou a Empresa.</exception>
    /// <exception cref="ValidationFailedException">Se a descrição ou o sítio na Internet não cumprirem a validação.</exception>
    public Task<CompanyPageDto> UpdatePageAsync(
        Guid recruiterId,
        CompanyPageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda o logótipo, em JPEG ou PNG até 2 MB, com nome gerado pelo servidor, e substitui o anterior (documentação
    /// da API v02, secção 6.4.6; RF047; RNF016; P01).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão, com a Empresa aprovada.</param>
    /// <param name="file">Ficheiro do campo <c>file</c>, ou <see langword="null"/> se faltar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O URL da API que serve o logótipo.</returns>
    /// <exception cref="FileRejectedException">ERR-11, se o ficheiro faltar ou não cumprir o formato ou a dimensão.</exception>
    /// <exception cref="ElementNotFoundException">ERR-20, se o Recrutador ainda não registou a Empresa.</exception>
    public Task<string> UploadLogoAsync(Guid recruiterId, IFormFile? file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta uma fotografia, em JPEG ou PNG até 5 MB, no fim da galeria, com nome gerado pelo servidor
    /// (documentação da API v02, secção 6.4.7; RF048; RNF016; P01, P13).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão, com a Empresa aprovada.</param>
    /// <param name="file">Ficheiro do campo <c>file</c>, ou <see langword="null"/> se faltar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O URL da API que serve a fotografia.</returns>
    /// <exception cref="FileRejectedException">ERR-11, se o ficheiro faltar ou não cumprir o formato ou a dimensão.</exception>
    /// <exception cref="ElementNotFoundException">ERR-20, se o Recrutador ainda não registou a Empresa.</exception>
    /// <exception cref="OperationConflictException">ERR-39, se a galeria já tiver 6 fotografias.</exception>
    public Task<string> AddGalleryPhotoAsync(Guid recruiterId, IFormFile? file, CancellationToken cancellationToken = default);
}
