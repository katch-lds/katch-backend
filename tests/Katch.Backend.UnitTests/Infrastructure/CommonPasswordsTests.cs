// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure;

namespace Katch.Backend.UnitTests.Infrastructure;

/// <summary>
/// Testes da lista de palavras-passe comuns (SEG-009).
/// </summary>
public sealed class CommonPasswordsTests
{
    /// <summary>
    /// Todas as entradas cumprem o P05: as que não cumprem nunca chegariam a ser comparadas com a lista.
    /// </summary>
    [Fact]
    public void EveryEntryPassesP05()
    {
        Assert.NotEmpty(CommonPasswords.All);
        Assert.All(CommonPasswords.All, password =>
        {
            Assert.True(password.Length >= 8, password);
            Assert.Contains(password, char.IsLetter);
            Assert.Contains(password, char.IsDigit);
        });
    }

    /// <summary>
    /// A comparação ignora maiúsculas e minúsculas, e uma palavra-passe fora da lista não é recusada.
    /// </summary>
    /// <param name="password">Palavra-passe.</param>
    /// <param name="expected">Se é comum.</param>
    [Theory]
    [InlineData("password1", true)]
    [InlineData("PassWord1", true)]
    [InlineData("QWERTY123", true)]
    [InlineData("katch2026", false)]
    [InlineData("Mar3-Azul-Faro", false)]
    public void ContainsIgnoresCase(string password, bool expected)
    {
        Assert.Equal(expected, CommonPasswords.Contains(password));
    }
}
