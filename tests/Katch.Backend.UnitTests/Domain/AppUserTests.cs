// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes das regras de estado da entidade <see cref="AppUser"/> (modelo de dados v02, secção 7.1).
/// </summary>
public sealed class AppUserTests
{
    private static readonly DateTimeOffset Registered = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = Registered.AddHours(1);

    /// <summary>
    /// Uma conta nova fica ativa, com as datas de registo e de alteração iguais ao instante do registo.
    /// </summary>
    [Fact]
    public void NewAccountIsActiveWithRegistrationDates()
    {
        var user = new AppUser("ana@katch.pt", "hash", UserType.Candidate, Registered, Registered);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("ana@katch.pt", user.Email);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(UserType.Candidate, user.UserType);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.Equal(Registered, user.TermsAcceptedAt);
        Assert.Equal(Registered, user.CreatedAt);
        Assert.Equal(Registered, user.UpdatedAt);
        Assert.True(user.CanSignIn());
    }

    /// <summary>
    /// A conta de Administrador não exige a aceitação das condições de utilização.
    /// </summary>
    [Fact]
    public void AdminAccountDoesNotRequireTerms()
    {
        var user = new AppUser("admin@katch.pt", "hash", UserType.Admin, null, Registered);

        Assert.Null(user.TermsAcceptedAt);
    }

    /// <summary>
    /// As contas de Candidato e de Recrutador exigem a aceitação das condições de utilização.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Recruiter)]
    public void CandidateAndRecruiterRequireTerms(UserType userType)
    {
        Assert.Throws<ArgumentException>(() => new AppUser("ana@katch.pt", "hash", userType, null, Registered));
    }

    /// <summary>
    /// O correio eletrónico rejeita os valores que não têm o formato local@domínio.
    /// </summary>
    /// <param name="email">Valor inválido.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("semarroba")]
    [InlineData("@katch.pt")]
    [InlineData("ana@")]
    [InlineData("a@b@katch.pt")]
    [InlineData("ana @katch.pt")]
    public void InvalidEmailIsRejected(string email)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AppUser(email, "hash", UserType.Candidate, Registered, Registered));
    }

    /// <summary>
    /// O resumo da palavra-passe não pode estar vazio.
    /// </summary>
    /// <param name="hash">Valor inválido.</param>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyPasswordHashIsRejected(string hash)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AppUser("ana@katch.pt", hash, UserType.Candidate, Registered, Registered));
    }

    /// <summary>
    /// O bloqueio de uma conta ativa de Candidato ou de Recrutador impede o início de sessão.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Recruiter)]
    public void BlockPreventsSignIn(UserType userType)
    {
        AppUser user = CreateUser(userType);

        user.Block(Later);

        Assert.Equal(AccountStatus.Blocked, user.Status);
        Assert.False(user.CanSignIn());
        Assert.Equal(Later, user.UpdatedAt);
    }

    /// <summary>
    /// A conta de Administrador nunca é bloqueada nem suspensa.
    /// </summary>
    [Fact]
    public void AdminCanNeitherBeBlockedNorSuspended()
    {
        AppUser admin = CreateUser(UserType.Admin);

        Assert.Throws<InvalidOperationException>(() => admin.Block(Later));
        Assert.Throws<InvalidOperationException>(() => admin.Suspend(Later));
        Assert.Equal(AccountStatus.Active, admin.Status);
        Assert.Equal(Registered, admin.UpdatedAt);
    }

    /// <summary>
    /// A suspensão só se aplica a contas de Candidato.
    /// </summary>
    [Fact]
    public void SuspendAppliesOnlyToCandidates()
    {
        AppUser candidate = CreateUser(UserType.Candidate);
        AppUser recruiter = CreateUser(UserType.Recruiter);

        candidate.Suspend(Later);

        Assert.Equal(AccountStatus.Suspended, candidate.Status);
        Assert.False(candidate.CanSignIn());
        Assert.Throws<InvalidOperationException>(() => recruiter.Suspend(Later));
        Assert.Equal(AccountStatus.Active, recruiter.Status);
    }

    /// <summary>
    /// Só uma conta ativa pode ser bloqueada ou suspensa.
    /// </summary>
    [Fact]
    public void OnlyActiveAccountCanBeBlockedOrSuspended()
    {
        AppUser blocked = CreateUser(UserType.Candidate);
        blocked.Block(Later);
        AppUser suspended = CreateUser(UserType.Candidate);
        suspended.Suspend(Later);

        Assert.Throws<InvalidOperationException>(() => blocked.Block(Later));
        Assert.Throws<InvalidOperationException>(() => blocked.Suspend(Later));
        Assert.Throws<InvalidOperationException>(() => suspended.Block(Later));
        Assert.Throws<InvalidOperationException>(() => suspended.Suspend(Later));
    }

    /// <summary>
    /// A reativação repõe a conta bloqueada ou suspensa e permite o início de sessão.
    /// </summary>
    /// <param name="suspend">Se a conta é suspensa em vez de bloqueada.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReactivateRestoresBlockedOrSuspendedAccount(bool suspend)
    {
        AppUser user = CreateUser(UserType.Candidate);
        if (suspend)
        {
            user.Suspend(Later);
        }
        else
        {
            user.Block(Later);
        }

        DateTimeOffset reactivated = Later.AddHours(1);
        user.Reactivate(reactivated);

        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.True(user.CanSignIn());
        Assert.Equal(reactivated, user.UpdatedAt);
    }

    /// <summary>
    /// Reativar uma conta que já está ativa é uma transição inválida.
    /// </summary>
    [Fact]
    public void ReactivatingActiveAccountIsRejected()
    {
        AppUser user = CreateUser(UserType.Recruiter);

        Assert.Throws<InvalidOperationException>(() => user.Reactivate(Later));
    }

    /// <summary>
    /// A substituição do resumo da palavra-passe atualiza a data de alteração.
    /// </summary>
    [Fact]
    public void SetPasswordHashReplacesHashAndUpdatesDate()
    {
        AppUser user = CreateUser(UserType.Candidate);

        user.SetPasswordHash("novo-hash", Later);

        Assert.Equal("novo-hash", user.PasswordHash);
        Assert.Equal(Later, user.UpdatedAt);
        Assert.Throws<ArgumentException>(() => user.SetPasswordHash(" ", Later));
        Assert.Equal("novo-hash", user.PasswordHash);
    }

    private static AppUser CreateUser(UserType userType)
    {
        DateTimeOffset? terms = userType == UserType.Admin ? null : Registered;
        return new AppUser("utilizador@katch.pt", "hash", userType, terms, Registered);
    }
}
