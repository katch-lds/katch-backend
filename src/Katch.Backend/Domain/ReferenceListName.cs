// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain;

/// <summary>
/// Regras da designação de uma competência ou de um benefício das listas pré-definidas (modelo de dados v02, secções
/// 6.7 e 6.9; RF099 a RF102): não vazia e com no máximo 100 caracteres. A unicidade sem distinção de maiúsculas depende
/// de outras linhas e é garantida pelos índices <c>uq_skill_name</c> e <c>uq_benefit_name</c> e pelos serviços.
/// </summary>
internal static class ReferenceListName
{
    /// <summary>
    /// Comprimento máximo da designação (DM-12).
    /// </summary>
    internal const int MaxLength = 100;

    /// <summary>
    /// Valida a designação e devolve-a sem os espaços nas extremidades.
    /// </summary>
    /// <param name="name">Designação indicada pelo Administrador.</param>
    /// <param name="parameterName">Nome do parâmetro, para a exceção.</param>
    /// <returns>A designação sem os espaços nas extremidades.</returns>
    /// <exception cref="ArgumentException">Se a designação estiver vazia ou tiver mais de 100 caracteres.</exception>
    internal static string Normalize(string name, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A designação é obrigatória.", parameterName);
        }

        string trimmed = name.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"A designação tem no máximo {MaxLength} caracteres.", parameterName);
        }

        return trimmed;
    }
}
