// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text;
using Microsoft.Extensions.Options;

namespace Katch.Backend.Infrastructure.Authentication;

/// <summary>
/// Valida a configuração JWT no arranque. As mensagens indicam como corrigir e nunca incluem valores.
/// </summary>
internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Secret) || Encoding.UTF8.GetByteCount(options.Secret) < JwtOptions.MinSecretBytes)
        {
            failures.Add(
                $"Falta o segredo JWT ou tem menos de {JwtOptions.MinSecretBytes} bytes. "
                + "Defina a variável de ambiente Jwt__Secret (configuração Jwt:Secret) com pelo menos "
                + $"{JwtOptions.MinSecretBytes} bytes; consulte a secção \"Autenticação JWT\" do README.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Issuer e Jwt:Audience não podem estar vazios.");
        }

        if (options.LifetimeHours <= 0)
        {
            failures.Add("Jwt:LifetimeHours tem de ser maior que zero.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
