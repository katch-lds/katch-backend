// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Domain;

/// <summary>
/// Conta de Recrutador (tabela <c>recruiter</c>): especialização de <see cref="AppUser"/> com a chave primária
/// partilhada. É criada na criação de conta, antes do registo da Empresa (RF037), à qual fica associada.
/// </summary>
public sealed class Recruiter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Recruiter"/> class a partir da conta de utilizador.
    /// </summary>
    /// <param name="user">Conta de utilizador do tipo <see cref="UserType.Recruiter"/>.</param>
    /// <exception cref="ArgumentException">Se a conta não for de Recrutador (RF085, RNF006).</exception>
    public Recruiter(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.UserType != UserType.Recruiter)
        {
            throw new ArgumentException("A conta tem de ser do tipo Recrutador.", nameof(user));
        }

        this.Id = user.Id;
        this.User = user;
    }

    private Recruiter()
    {
        this.User = null!;
    }

    /// <summary>
    /// Gets the identifier of the recruiter (<c>id</c>), primary key and foreign key to <c>app_user</c>.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the account of the user that this recruiter specializes.
    /// </summary>
    public AppUser User { get; private set; }
}
