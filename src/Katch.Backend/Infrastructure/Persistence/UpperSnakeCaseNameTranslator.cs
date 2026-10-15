// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Npgsql;
using Npgsql.NameTranslation;

namespace Katch.Backend.Infrastructure.Persistence;

/// <summary>
/// Converte os nomes dos membros das enumerações nos valores dos tipos enumerados do modelo de dados,
/// em maiúsculas e separados por <c>_</c> (por exemplo, <c>ServiceProvision</c> passa a <c>SERVICE_PROVISION</c>).
/// </summary>
public sealed class UpperSnakeCaseNameTranslator : INpgsqlNameTranslator
{
    private readonly NpgsqlSnakeCaseNameTranslator snakeCase = new();

    /// <inheritdoc/>
    public string TranslateTypeName(string clrName)
    {
        return this.snakeCase.TranslateTypeName(clrName);
    }

    /// <inheritdoc/>
    public string TranslateMemberName(string clrName)
    {
        return this.snakeCase.TranslateMemberName(clrName).ToUpperInvariant();
    }
}
