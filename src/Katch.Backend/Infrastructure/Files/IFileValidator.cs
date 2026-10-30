// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Validação dos ficheiros enviados (documentação da API v02, secção 2.6; RNF015, RNF016).
/// </summary>
public interface IFileValidator
{
    /// <summary>
    /// Valida a extensão, a assinatura do conteúdo (primeiros bytes) e a dimensão do ficheiro para o tipo indicado.
    /// </summary>
    /// <param name="file">Ficheiro enviado; <see langword="null"/> se o campo faltar.</param>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <returns>O resultado, com o motivo do ERR-11 se for recusado.</returns>
    public ValidationResult Validate(IFormFile? file, FileKind kind);
}
