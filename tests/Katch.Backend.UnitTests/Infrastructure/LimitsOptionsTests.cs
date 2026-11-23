// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using System.Reflection;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Hosting;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Katch.Backend.UnitTests.Infrastructure;

/// <summary>
/// Testes dos limites globais (<see cref="LimitsOptions"/>) e da verificação de prontidão da base de dados.
/// </summary>
public sealed class LimitsOptionsTests
{
    /// <summary>
    /// Gets each limit with a zero and a negative value.
    /// </summary>
    public static TheoryData<string, int> NonPositiveLimits
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (PropertyInfo property in typeof(LimitsOptions).GetProperties())
            {
                data.Add(property.Name, 0);
                data.Add(property.Name, -7);
            }

            return data;
        }
    }

    /// <summary>
    /// Os valores por omissão são válidos e o corpo fica nos 64 KB da documentação da API (secção 4.2).
    /// </summary>
    [Fact]
    public void DefaultsAreValid()
    {
        var options = new LimitsOptions();

        Assert.True(new LimitsOptionsValidator().Validate(null, options).Succeeded);
        Assert.Equal(65_536, options.MaxRequestBodyBytes);
    }

    /// <summary>
    /// Cada limite nulo ou negativo para a aplicação com uma mensagem que indica a variável, sem repetir o valor.
    /// </summary>
    /// <param name="property">Limite configurado.</param>
    /// <param name="value">Valor configurado.</param>
    [Theory]
    [MemberData(nameof(NonPositiveLimits))]
    public void NonPositiveLimitIsRejected(string property, int value)
    {
        var options = new LimitsOptions();
        PropertyInfo limit = typeof(LimitsOptions).GetProperty(property)!;
        limit.SetValue(options, Convert.ChangeType(value, limit.PropertyType, CultureInfo.InvariantCulture));

        ValidateOptionsResult result = new LimitsOptionsValidator().Validate(null, options);

        Assert.Contains(result.Failures!, failure => failure.Contains($"Limits__{property}", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Failures!,
            failure => failure.Contains(value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    /// <summary>
    /// O bloqueio máximo não pode ser inferior ao primeiro.
    /// </summary>
    [Fact]
    public void MaxLockoutBelowTheFirstLockoutIsRejected()
    {
        ValidateOptionsResult result = new LimitsOptionsValidator().Validate(
            null,
            new LimitsOptions { LockoutSeconds = 120, MaxLockoutSeconds = 60 });

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Limits__MaxLockoutSeconds", failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sem ligação à base de dados, a verificação de prontidão falha.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ReadinessIsUnhealthyWithoutDatabase()
    {
        var builder = new DbContextOptionsBuilder<KatchDbContext>();
        builder.UseKatchPostgres("Host=127.0.0.1;Port=1;Timeout=1");
        using var context = new KatchDbContext(builder.Options);

        HealthCheckResult result = await new DatabaseHealthCheck(context).CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
