// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Cálculo e verificação do resumo das palavras-passe (modelo de classes do backend v02, secção 5.2; RNF003). A
/// palavra-passe nunca é guardada nem devolvida.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Calcula o resumo irreversível da palavra-passe, com valor aleatório próprio.
    /// </summary>
    /// <param name="password">Palavra-passe em claro.</param>
    /// <returns>O resumo, com no máximo 255 caracteres.</returns>
    public string Hash(string password);

    /// <summary>
    /// Verifica se a palavra-passe corresponde ao resumo guardado.
    /// </summary>
    /// <param name="hash">Resumo guardado.</param>
    /// <param name="password">Palavra-passe em claro.</param>
    /// <returns><see langword="true"/> se corresponder.</returns>
    public bool Verify(string hash, string password);
}
