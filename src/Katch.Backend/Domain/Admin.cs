// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Domain;

/// <summary>
/// Conta de Administrador (tabela <c>admin</c>): especialização de <see cref="AppUser"/> com a chave primária
/// partilhada. Não há registo de Administrador na plataforma (RF083): as contas são criadas pelos dados iniciais
/// (modelo de dados v02, secções 6.2 e 9).
/// </summary>
public sealed class Admin
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Admin"/> class a partir da conta de utilizador.
    /// </summary>
    /// <param name="user">Conta de utilizador do tipo <see cref="UserType.Admin"/>.</param>
    /// <exception cref="ArgumentException">Se a conta não for de Administrador (RF085, RNF006).</exception>
    public Admin(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.UserType != UserType.Admin)
        {
            throw new ArgumentException("A conta tem de ser do tipo Administrador.", nameof(user));
        }

        this.Id = user.Id;
        this.User = user;
    }

    private Admin()
    {
        this.User = null!;
    }

    /// <summary>
    /// Gets the identifier of the administrator (<c>id</c>), primary key and foreign key to <c>app_user</c>.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the account of the user that this administrator specializes.
    /// </summary>
    public AppUser User { get; private set; }
}
