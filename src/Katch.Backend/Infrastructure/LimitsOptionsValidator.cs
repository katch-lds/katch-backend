// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Microsoft.Extensions.Options;

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Valida os limites globais no arranque. Cada mensagem indica a variável a corrigir e nunca inclui o valor lido.
/// </summary>
internal sealed class LimitsOptionsValidator : IValidateOptions<LimitsOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, LimitsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        RequirePositive(options.MaxRequestBodyBytes, nameof(LimitsOptions.MaxRequestBodyBytes), failures);
        RequirePositive(options.RateLimitWindowSeconds, nameof(LimitsOptions.RateLimitWindowSeconds), failures);
        RequirePositive(options.LoginPermitLimit, nameof(LimitsOptions.LoginPermitLimit), failures);
        RequirePositive(options.RegistrationPermitLimit, nameof(LimitsOptions.RegistrationPermitLimit), failures);
        RequirePositive(options.LocationsPermitLimit, nameof(LimitsOptions.LocationsPermitLimit), failures);
        RequirePositive(options.UserPermitLimit, nameof(LimitsOptions.UserPermitLimit), failures);
        RequirePositive(options.MaxFailedLogins, nameof(LimitsOptions.MaxFailedLogins), failures);
        RequirePositive(options.LockoutSeconds, nameof(LimitsOptions.LockoutSeconds), failures);
        RequirePositive(options.MaxLockoutSeconds, nameof(LimitsOptions.MaxLockoutSeconds), failures);

        if (options.MaxLockoutSeconds < options.LockoutSeconds)
        {
            failures.Add(
                "O bloqueio máximo não pode ser inferior ao primeiro bloqueio. Corrija as variáveis de ambiente "
                + $"{LimitsOptions.SectionName}__{nameof(LimitsOptions.MaxLockoutSeconds)} e "
                + $"{LimitsOptions.SectionName}__{nameof(LimitsOptions.LockoutSeconds)}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void RequirePositive(long value, string property, List<string> failures)
    {
        if (value < 1)
        {
            failures.Add(
                $"O limite {property} tem de ser um número inteiro positivo. Corrija a variável de ambiente "
                + $"{LimitsOptions.SectionName}__{property} (configuração {LimitsOptions.SectionName}:{property}).");
        }
    }
}
