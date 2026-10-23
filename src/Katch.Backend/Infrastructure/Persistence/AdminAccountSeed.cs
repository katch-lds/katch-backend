// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.AspNetCore.Identity;

namespace Katch.Backend.Infrastructure.Persistence;

/// <summary>
/// Dados iniciais da conta de Administrador (modelo de dados v02, secções 6.2 e 9; RF083): o Administrador não tem
/// registo na plataforma, por isso a migration <c>AddAdmin</c> cria uma conta ativa com o correio eletrónico
/// <see cref="Email"/>.
/// </summary>
/// <remarks>
/// A palavra-passe vem da variável de ambiente <see cref="PasswordVariable"/> e nunca do repositório (RI, secção 14.2).
/// Só o resumo é guardado, calculado pelo <see cref="PasswordHasher{TUser}"/> do ASP.NET Core Identity, com valor
/// aleatório próprio da conta (RNF003; modelo de classes do backend v02, secção 5.2, <c>IdentityPasswordHasher</c>).
/// </remarks>
public static class AdminAccountSeed
{
    /// <summary>
    /// Correio eletrónico da conta de Administrador criada pelos dados iniciais (dados fictícios, P18).
    /// </summary>
    public const string Email = "admin@katch.pt";

    /// <summary>
    /// Nome da variável de ambiente com a palavra-passe da conta de Administrador.
    /// </summary>
    public const string PasswordVariable = "Katch__AdminPassword";

    /// <summary>
    /// Instrução que apaga as contas de Administrador, usada ao reverter a migration depois de apagada a tabela
    /// <c>admin</c>.
    /// </summary>
    public const string DeleteSql = "DELETE FROM app_user WHERE user_type = 'ADMIN';";

    private const int MinPasswordLength = 8;

    /// <summary>
    /// Lê a palavra-passe da conta de Administrador da variável de ambiente <see cref="PasswordVariable"/>.
    /// </summary>
    /// <returns>A palavra-passe definida na variável.</returns>
    /// <exception cref="InvalidOperationException">Se a variável não estiver definida ou estiver vazia.</exception>
    public static string ReadPasswordFromEnvironment()
    {
        return ReadPassword(Environment.GetEnvironmentVariable);
    }

    /// <summary>
    /// Valida a palavra-passe segundo o P05 e calcula o resumo irreversível, com valor aleatório próprio.
    /// </summary>
    /// <param name="password">Palavra-passe da conta de Administrador.</param>
    /// <returns>O resumo da palavra-passe, com no máximo 255 caracteres.</returns>
    /// <exception cref="ArgumentException">
    /// Se a palavra-passe tiver menos de 8 caracteres ou não tiver pelo menos uma letra e um algarismo (P05).
    /// </exception>
    public static string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        bool valid = password.Length >= MinPasswordLength
            && password.Any(char.IsLetter)
            && password.Any(char.IsDigit);

        if (!valid)
        {
            throw new ArgumentException(
                "A palavra-passe do Administrador deve ter pelo menos 8 caracteres, com uma letra e um algarismo (P05).",
                nameof(password));
        }

        var hasher = new PasswordHasher<AppUser>();
        return hasher.HashPassword(null!, password);
    }

    /// <summary>
    /// Cria a instrução que insere a conta de Administrador em <c>app_user</c> (tipo <c>ADMIN</c>, estado
    /// <c>ACTIVE</c>, sem aceitação das condições) e a respetiva especialização em <c>admin</c>.
    /// </summary>
    /// <param name="passwordHash">Resumo da palavra-passe, calculado por <see cref="HashPassword"/>.</param>
    /// <returns>A instrução SQL.</returns>
    /// <exception cref="ArgumentException">Se o resumo estiver vazio ou passar dos 255 caracteres.</exception>
    public static string BuildInsertSql(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(passwordHash.Length, AppUser.MaxLength);

        string hash = passwordHash.Replace("'", "''", StringComparison.Ordinal);

        return "WITH new_user AS ("
            + " INSERT INTO app_user (email, password_hash, user_type, status)"
            + $" VALUES ('{Email}', '{hash}', 'ADMIN', 'ACTIVE')"
            + " RETURNING id)"
            + " INSERT INTO admin (id) SELECT id FROM new_user;";
    }

    /// <summary>
    /// Lê a palavra-passe através da função indicada, para que os testes não alterem as variáveis do processo.
    /// </summary>
    /// <param name="getVariable">Função que devolve o valor de uma variável de ambiente.</param>
    /// <returns>A palavra-passe definida na variável.</returns>
    /// <exception cref="InvalidOperationException">Se a variável não estiver definida ou estiver vazia.</exception>
    internal static string ReadPassword(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);

        string? password = getVariable(PasswordVariable);

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"Falta a palavra-passe da conta de Administrador. Defina a variável de ambiente {PasswordVariable}.");
        }

        return password;
    }
}
