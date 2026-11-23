// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Collections.Frozen;

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Palavras-passe comuns recusadas mesmo cumprindo o P05 (SEG-009): as mais frequentes das listas públicas de fugas de
/// dados com pelo menos 8 caracteres, uma letra e um algarismo, e variantes portuguesas óbvias. A comparação ignora
/// maiúsculas e minúsculas. Só entram valores que o P05 aceita, porque os restantes já são recusados por ele.
/// </summary>
public static class CommonPasswords
{
    private static readonly FrozenSet<string> Passwords = new[]
    {
        "password1", "password12", "password123", "password1234", "passw0rd", "p4ssw0rd", "pa55word",
        "qwerty123", "qwerty1234", "qwerty12345", "1qaz2wsx", "1q2w3e4r", "1q2w3e4r5t", "q1w2e3r4", "q1w2e3r4t5",
        "zaq12wsx", "asdf1234", "asdfgh123", "zxcvbnm1", "qazwsx123",
        "abc12345", "abcd1234", "abc123456", "a1234567", "a12345678", "a123456789", "1234567a", "12345678a",
        "123456789a", "12345abc", "123abc123", "aa123456", "aaa11111", "a1b2c3d4", "1a2b3c4d",
        "iloveyou1", "iloveyou2", "welcome1", "welcome123", "letmein1", "letmein123", "monkey123", "dragon123",
        "football1", "baseball1", "superman1", "batman123", "shadow123", "master123", "sunshine1", "princess1",
        "trustno1", "michael1", "charlie1", "jordan23", "admin123", "admin1234", "administrador1", "root1234",
        "test1234", "teste123", "teste1234", "user1234", "login123", "changeme1", "secret123",
        "palavrapasse1", "palavra123", "senha123", "senha1234", "portugal1", "portugal123", "lisboa123",
        "porto123", "benfica1", "benfica123", "sporting1", "sporting123", "fcporto1", "qwertyuiop1", "123qweasd",
        "qwe123qwe",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets every password of the list, for the tests that confirm that each one passes the P05.
    /// </summary>
    internal static IReadOnlyCollection<string> All => Passwords;

    /// <summary>
    /// Indica se a palavra-passe está na lista de palavras-passe comuns.
    /// </summary>
    /// <param name="password">Palavra-passe em claro.</param>
    /// <returns><see langword="true"/> se for uma palavra-passe comum.</returns>
    public static bool Contains(string password)
    {
        return Passwords.Contains(password);
    }
}
