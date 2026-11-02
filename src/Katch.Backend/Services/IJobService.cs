// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Gestão das vagas pelo Recrutador da Empresa aprovada (modelo de classes do backend v02, secção 5.1; UC09, UC10). A
/// Empresa é sempre a do Recrutador da credencial de sessão; uma vaga de outra Empresa dá
/// <see cref="ElementForbiddenException"/> (RF059) e uma vaga inexistente ou eliminada dá
/// <see cref="ElementNotFoundException"/>. Também acrescenta fotografias à vaga (secção 6.5.9, I105); o encerramento
/// automático (<c>CloseExpiredAsync</c>) é acrescentado pela Issue que o implementa (I115).
/// </summary>
public interface IJobService
{
    /// <summary>
    /// Lista as vagas da Empresa do Recrutador, sem as eliminadas, da mais recente para a mais antiga, com os interesses
    /// em espera de cada uma (documentação da API v02, secção 6.5.1; RF059, RF060).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As vagas da Empresa.</returns>
    public Task<List<JobDto>> ListOwnAsync(Guid recruiterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria uma vaga não publicada na Empresa do Recrutador e regista a operação (documentação da API v02, secção 6.5.2;
    /// RF050, RF051, RF112).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="request">Dados da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A vaga criada.</returns>
    /// <exception cref="ValidationFailedException">Com todos os campos em erro.</exception>
    public Task<JobDto> CreateAsync(Guid recruiterId, JobRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Substitui os dados editáveis de uma vaga que não está encerrada, sem mudar o estado, e regista a operação
    /// (documentação da API v02, secção 6.5.3; RF054, RF051, RF112).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="request">Dados completos da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A vaga alterada.</returns>
    /// <exception cref="OperationConflictException">ERR-30, se a vaga estiver encerrada.</exception>
    public Task<JobDto> UpdateAsync(Guid recruiterId, Guid jobId, JobRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publica uma vaga não publicada cuja data-limite não foi atingida (documentação da API v02, secção 6.5.4; RF053).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A vaga publicada.</returns>
    /// <exception cref="OperationConflictException">ERR-30, fora de não publicada ou com a data-limite atingida.</exception>
    public Task<JobDto> PublishAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspende uma vaga publicada; os interesses mantêm-se (documentação da API v02, secção 6.5.5; RF055, RF113).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A vaga suspensa.</returns>
    /// <exception cref="OperationConflictException">ERR-30, fora de publicada.</exception>
    public Task<JobDto> SuspendAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Volta a publicar uma vaga suspensa, sem mudar a data da primeira publicação (documentação da API v02, secção
    /// 6.5.6; RF115).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A vaga publicada.</returns>
    /// <exception cref="OperationConflictException">ERR-30, fora de suspensa.</exception>
    public Task<JobDto> RepublishAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encerra definitivamente uma vaga; os interesses e os matches mantêm-se (documentação da API v02, secção 6.5.7;
    /// RF056, RF113).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A vaga encerrada.</returns>
    /// <exception cref="OperationConflictException">ERR-30, se a vaga já estiver encerrada.</exception>
    public Task<JobDto> CloseAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina logicamente uma vaga sem interesses registados (documentação da API v02, secção 6.5.8; RF058, RF114).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a vaga foi eliminada.</returns>
    /// <exception cref="OperationConflictException">ERR-38, se a vaga tiver pelo menos um interesse.</exception>
    public Task DeleteAsync(Guid recruiterId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta uma fotografia ao fim da lista da vaga (documentação da API v02, secção 6.5.9; RF052; RNF015,
    /// RNF016; P01, P13). As verificações seguem a ordem: titularidade, estado, limite e ficheiro; o limite é decidido
    /// por uma atualização atómica, por isso pedidos simultâneos nunca ultrapassam as 5 fotografias nem deixam
    /// ficheiros órfãos.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="file">Ficheiro enviado no campo <c>file</c>; <see langword="null"/> se faltar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O URL da API que serve a fotografia.</returns>
    /// <exception cref="OperationConflictException">ERR-30, se a vaga estiver encerrada; ERR-39, se já tiver 5 fotografias.</exception>
    /// <exception cref="FileRejectedException">ERR-11, se o ficheiro faltar, não for JPEG nem PNG ou exceder 5 MB.</exception>
    public Task<string> AddPhotoAsync(Guid recruiterId, Guid jobId, IFormFile? file, CancellationToken cancellationToken = default);
}
