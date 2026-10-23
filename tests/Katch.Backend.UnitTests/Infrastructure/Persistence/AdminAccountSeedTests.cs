// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Katch.Backend.UnitTests.Infrastructure.Persistence;

/// <summary>
/// Testes dos dados iniciais da conta de Administrador (modelo de dados v02, secção 9; RF083): palavra-passe lida da
/// variável de ambiente, validada pelo P05 e guardada só como resumo (RNF003; RI, secção 14.2).
/// </summary>
public sealed class AdminAccountSeedTests
{
    private const string Password = "Katch2026admin";

    /// <summary>
    /// O resumo não contém a palavra-passe, cabe em <c>varchar(255)</c> e é verificado pelo mesmo
    /// <see cref="PasswordHasher{TUser}"/> que valida o início de sessão.
    /// </summary>
    [Fact]
    public void HashIsVerifiableAndDoesNotContainThePassword()
    {
        string hash = AdminAccountSeed.HashPassword(Password);

        Assert.DoesNotContain(Password, hash, StringComparison.Ordinal);
        Assert.True(hash.Length <= AppUser.MaxLength);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<AppUser>().VerifyHashedPassword(null!, hash, Password));
    }

    /// <summary>
    /// Cada cálculo usa um valor aleatório próprio, por isso a mesma palavra-passe dá resumos diferentes.
    /// </summary>
    [Fact]
    public void SamePasswordGivesDifferentHashes()
    {
        Assert.NotEqual(AdminAccountSeed.HashPassword(Password), AdminAccountSeed.HashPassword(Password));
    }

    /// <summary>
    /// Uma palavra-passe fora do P05 (mínimo de 8 caracteres, com uma letra e um algarismo) é rejeitada.
    /// </summary>
    /// <param name="password">Palavra-passe que não cumpre o P05.</param>
    [Theory]
    [InlineData("abc1234")]
    [InlineData("abcdefgh")]
    [InlineData("12345678")]
    [InlineData("   ")]
    public void PasswordOutsideP05IsRejected(string password)
    {
        Assert.ThrowsAny<ArgumentException>(() => AdminAccountSeed.HashPassword(password));
    }

    /// <summary>
    /// A palavra-passe vem da variável <see cref="AdminAccountSeed.PasswordVariable"/>.
    /// </summary>
    [Fact]
    public void PasswordIsReadFromTheVariable()
    {
        string password = AdminAccountSeed.ReadPassword(
            name => name == AdminAccountSeed.PasswordVariable ? Password : null);

        Assert.Equal(Password, password);
    }

    /// <summary>
    /// Sem a variável, ou com a variável vazia, a migration não cria uma conta sem palavra-passe.
    /// </summary>
    /// <param name="value">Valor da variável de ambiente.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MissingVariableIsRejected(string? value)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => AdminAccountSeed.ReadPassword(_ => value));

        Assert.Contains(AdminAccountSeed.PasswordVariable, error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A instrução cria a conta em <c>app_user</c> com o tipo <c>ADMIN</c> e o estado <c>ACTIVE</c> e a especialização
    /// em <c>admin</c> com o mesmo identificador.
    /// </summary>
    [Fact]
    public void InsertCreatesAnActiveAdminAccountAndItsSpecialization()
    {
        string sql = AdminAccountSeed.BuildInsertSql("resumo");

        Assert.Equal(
            "WITH new_user AS ( INSERT INTO app_user (email, password_hash, user_type, status)"
            + " VALUES ('admin@katch.pt', 'resumo', 'ADMIN', 'ACTIVE') RETURNING id)"
            + " INSERT INTO admin (id) SELECT id FROM new_user;",
            sql);
    }

    /// <summary>
    /// As plicas do resumo são duplicadas, para que o valor não termine o literal SQL.
    /// </summary>
    [Fact]
    public void QuotesInTheHashAreEscaped()
    {
        string sql = AdminAccountSeed.BuildInsertSql("a'b");

        Assert.Contains("'a''b'", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Um resumo vazio ou maior do que a coluna <c>password_hash</c> é rejeitado.
    /// </summary>
    [Fact]
    public void InvalidHashIsRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => AdminAccountSeed.BuildInsertSql(string.Empty));
        Assert.ThrowsAny<ArgumentException>(() => AdminAccountSeed.BuildInsertSql(new string('x', AppUser.MaxLength + 1)));
    }
}
