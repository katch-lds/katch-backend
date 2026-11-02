// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Files;

namespace Katch.Backend.Services;

/// <summary>
/// Obtenção dos ficheiros guardados pela API (documentação da API v02, secção 6.9; modelo de classes do backend v02,
/// secção 5.1; RNF007, RF061).
/// </summary>
public interface IFileAccessService
{
    /// <summary>
    /// Obtém um ficheiro, se a conta tiver acesso: os logótipos e as fotografias da Empresa e das vagas, a qualquer
    /// conta com sessão iniciada; a fotografia de perfil, ao próprio Candidato e ao Recrutador de uma Empresa em cuja
    /// vaga o Candidato tem um interesse registado. O curriculum vitae nunca é obtido por esta operação.
    /// </summary>
    /// <param name="userId">Identificador da conta da sessão.</param>
    /// <param name="userType">Tipo da conta da sessão.</param>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <param name="fileName">Nome do ficheiro, tal como na rota.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O ficheiro, com o tipo de conteúdo real.</returns>
    /// <exception cref="ElementNotFoundException">
    /// Sempre com a mesma resposta, se o nome for inválido ou não existir, for de outro tipo, o tipo não puder ser
    /// obtido ou a conta não tiver acesso, para não revelar a existência do ficheiro.
    /// </exception>
    public Task<FileContent> GetAsync(
        Guid userId,
        UserType userType,
        FileKind kind,
        string fileName,
        CancellationToken cancellationToken = default);
}
