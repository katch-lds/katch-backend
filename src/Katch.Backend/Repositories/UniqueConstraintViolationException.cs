// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Repositories;

/// <summary>
/// A gravação violou um índice único do PostgreSQL (SQLSTATE <c>23505</c>), por exemplo quando dois pedidos registam
/// o mesmo correio eletrónico ao mesmo tempo e ambos passam a verificação prévia.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueConstraintViolationException"/> class.
    /// </summary>
    /// <param name="constraintName">Nome do índice ou da restrição violada, por exemplo <c>uq_app_user_email</c>.</param>
    /// <param name="innerException">Exceção original do Entity Framework Core.</param>
    public UniqueConstraintViolationException(string constraintName, Exception innerException)
        : base($"Violação do índice único {constraintName}.", innerException)
    {
        this.ConstraintName = constraintName;
    }

    /// <summary>
    /// Gets the name of the violated unique index or constraint.
    /// </summary>
    public string ConstraintName { get; }
}
