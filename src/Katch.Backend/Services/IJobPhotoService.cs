// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// Carregamento das fotografias de uma vaga pelo Recrutador da Empresa aprovada (documentação da API v02, secção
/// 6.5.9; RF052; P01, P13). É um serviço próprio para não alterar a construção do <see cref="IJobService"/>.
/// </summary>
public interface IJobPhotoService
{
    /// <summary>
    /// Acrescenta uma fotografia ao fim da lista da vaga (documentação da API v02, secção 6.5.9; RF052; RNF015,
    /// RNF016). As verificações seguem a ordem: titularidade, estado, limite e ficheiro.
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador da credencial de sessão.</param>
    /// <param name="jobId">Identificador da vaga.</param>
    /// <param name="file">Ficheiro enviado no campo <c>file</c>; <see langword="null"/> se faltar.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O URL da API que serve a fotografia.</returns>
    /// <exception cref="ElementNotFoundException">ERR-20, se a vaga não existir ou estiver eliminada.</exception>
    /// <exception cref="ElementForbiddenException">ERR-08, se a vaga for de outra Empresa.</exception>
    /// <exception cref="OperationConflictException">ERR-30, se a vaga estiver encerrada; ERR-39, se já tiver 5 fotografias.</exception>
    /// <exception cref="FileRejectedException">ERR-11, se o ficheiro faltar, não for JPEG nem PNG ou exceder 5 MB.</exception>
    public Task<string> AddPhotoAsync(Guid recruiterId, Guid jobId, IFormFile? file, CancellationToken cancellationToken = default);
}
