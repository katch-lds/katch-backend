// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="Admin"/>, especialização de <see cref="AppUser"/> (modelo de dados v02, secção 6.2).
/// </summary>
public sealed class AdminTests
{
    private static readonly DateTimeOffset Registered = new(2026, 10, 22, 18, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// O Administrador partilha o identificador da conta de utilizador, que é ativa e não exige a aceitação das
    /// condições de utilização.
    /// </summary>
    [Fact]
    public void AdminSharesTheIdentifierOfTheAccount()
    {
        var user = new AppUser("admin@katch.pt", "hash", UserType.Admin, null, Registered);

        var admin = new Admin(user);

        Assert.Equal(user.Id, admin.Id);
        Assert.Same(user, admin.User);
        Assert.Equal(AccountStatus.Active, admin.User.Status);
        Assert.Null(admin.User.TermsAcceptedAt);
    }

    /// <summary>
    /// Só uma conta do tipo Administrador pode ser especializada em Administrador.
    /// </summary>
    /// <param name="userType">Tipo de conta que não é de Administrador.</param>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Recruiter)]
    public void AccountOfAnotherTypeIsRejected(UserType userType)
    {
        var user = new AppUser("outro@katch.pt", "hash", userType, Registered, Registered);

        Assert.Throws<ArgumentException>(() => new Admin(user));
    }

    /// <summary>
    /// A conta de utilizador é obrigatória.
    /// </summary>
    [Fact]
    public void NullAccountIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Admin(null!));
    }
}
