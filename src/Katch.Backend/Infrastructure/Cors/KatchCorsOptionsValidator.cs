// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.Extensions.Options;

namespace Katch.Backend.Infrastructure.Cors;

/// <summary>
/// Valida as origens do CORS no arranque: cada uma tem de ser uma origem <c>http</c> ou <c>https</c> exata (esquema,
/// anfitrião e porto opcional), para que «*», caminhos ou valores mal escritos parem a aplicação com uma mensagem clara.
/// As mensagens não repetem o valor lido.
/// </summary>
internal sealed class KatchCorsOptionsValidator : IValidateOptions<KatchCorsOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, KatchCorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IReadOnlyList<string> origins = options.ParseOrigins();
        var failures = new List<string>();
        for (int index = 0; index < origins.Count; index++)
        {
            if (!IsExactOrigin(origins[index]))
            {
                failures.Add(
                    $"A origem n.º {index + 1} de Cors__AllowedOrigins (configuração Cors:AllowedOrigins) não é válida. "
                    + "Cada origem tem de ser http:// ou https:// seguido do anfitrião e, se preciso, do porto, sem caminho "
                    + "nem «*» (por exemplo http://localhost:4173); separe as origens por vírgulas.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Indica se o valor é uma origem exata: URI absoluto <c>http</c> ou <c>https</c>, sem credenciais, caminho, consulta
    /// nem fragmento (só se aceita a barra final, que é retirada).
    /// </summary>
    /// <param name="value">Valor configurado.</param>
    /// <returns><see langword="true"/> se for uma origem exata.</returns>
    internal static bool IsExactOrigin(string value) =>
        !value.Contains('*', StringComparison.Ordinal)
        && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.UserInfo.Length == 0
        && uri.AbsolutePath == "/"
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0;
}
