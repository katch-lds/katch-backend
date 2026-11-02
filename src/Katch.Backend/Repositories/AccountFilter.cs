// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Repositories;

/// <summary>
/// Critério da lista de contas de <see cref="IUserRepository.ListAsync"/> (modelo de classes do backend v02, secção 4).
/// Um campo nulo não restringe a lista. A documentação da API v02 não prevê filtros na lista do Administrador (secção 11,
/// L-02), que usa sempre <see cref="All"/>.
/// </summary>
/// <param name="UserType">Só as contas deste tipo, ou todas se for nulo.</param>
/// <param name="Status">Só as contas neste estado, ou todas se for nulo.</param>
public sealed record AccountFilter(UserType? UserType = null, AccountStatus? Status = null)
{
    /// <summary>
    /// Gets o critério sem restrições: todas as contas.
    /// </summary>
    public static AccountFilter All { get; } = new();
}
