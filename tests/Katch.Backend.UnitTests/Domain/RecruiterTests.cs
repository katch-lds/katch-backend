// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="Recruiter"/>, especialização de <see cref="AppUser"/> (modelo de dados v02, secção 6.3).
/// </summary>
public sealed class RecruiterTests
{
    private static readonly DateTimeOffset Registered = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// O Recrutador partilha o identificador da conta de utilizador.
    /// </summary>
    [Fact]
    public void RecruiterSharesTheIdentifierOfTheAccount()
    {
        var user = new AppUser("recrutador@katch.pt", "hash", UserType.Recruiter, Registered, Registered);

        var recruiter = new Recruiter(user);

        Assert.Equal(user.Id, recruiter.Id);
        Assert.Same(user, recruiter.User);
    }

    /// <summary>
    /// Só uma conta do tipo Recrutador pode ser especializada em Recrutador.
    /// </summary>
    /// <param name="userType">Tipo de conta que não é de Recrutador.</param>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Admin)]
    public void AccountOfAnotherTypeIsRejected(UserType userType)
    {
        DateTimeOffset? terms = userType == UserType.Admin ? null : Registered;
        var user = new AppUser("outro@katch.pt", "hash", userType, terms, Registered);

        Assert.Throws<ArgumentException>(() => new Recruiter(user));
    }

    /// <summary>
    /// A conta de utilizador é obrigatória.
    /// </summary>
    [Fact]
    public void NullAccountIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Recruiter(null!));
    }
}
