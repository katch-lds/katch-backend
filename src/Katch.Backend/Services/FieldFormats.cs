// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.RegularExpressions;

namespace Katch.Backend.Services;

/// <summary>
/// Formatos do P04 usados na validação automática dos serviços, iguais aos <c>CHECK</c> do modelo de dados v02 (secção
/// 5.2): correio eletrónico local@domínio e telefone ou NIF com exatamente 9 algarismos.
/// </summary>
internal static partial class FieldFormats
{
    /// <summary>
    /// Mesma regra dos <c>CHECK</c> de <c>app_user.email</c> e <c>company.contact_email</c>: exatamente um <c>@</c>,
    /// com texto sem espaços de cada lado.
    /// </summary>
    /// <param name="email">Correio eletrónico, já sem os espaços nas extremidades.</param>
    /// <returns><see langword="true"/> se tiver o formato local@domínio.</returns>
    public static bool IsEmail(string email)
    {
        int at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0
            && at == email.LastIndexOf('@')
            && at < email.Length - 1
            && !email.Any(char.IsWhiteSpace);
    }

    /// <summary>
    /// Indica se o texto tem exatamente 9 algarismos de 0 a 9 (telefone e NIF, P04).
    /// </summary>
    /// <param name="value">Texto, já sem os espaços nas extremidades.</param>
    /// <returns><see langword="true"/> se tiver exatamente 9 algarismos.</returns>
    public static bool IsNineDigits(string value)
    {
        return NineDigitsPattern().IsMatch(value);
    }

    [GeneratedRegex(@"^[0-9]{9}\z")]
    private static partial Regex NineDigitsPattern();
}
