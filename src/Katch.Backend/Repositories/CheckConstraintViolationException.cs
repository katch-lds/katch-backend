// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Repositories;

/// <summary>
/// A gravação violou uma restrição <c>CHECK</c> do PostgreSQL (SQLSTATE <c>23514</c>), por exemplo quando a base, com
/// outra locale, avalia uma classe de caracteres de forma diferente da validação do domínio.
/// </summary>
public sealed class CheckConstraintViolationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckConstraintViolationException"/> class.
    /// </summary>
    /// <param name="constraintName">Nome da restrição violada, por exemplo <c>ck_candidate_custom_skill_label</c>.</param>
    /// <param name="innerException">Exceção original do Entity Framework Core.</param>
    public CheckConstraintViolationException(string constraintName, Exception innerException)
        : base($"Violação da restrição {constraintName}.", innerException)
    {
        this.ConstraintName = constraintName;
    }

    /// <summary>
    /// Gets the name of the violated check constraint.
    /// </summary>
    public string ConstraintName { get; }
}
