// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

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
    /// Um limite nulo ou negativo para a aplicação com uma mensagem que indica a variável, sem repetir o valor.
    /// </summary>
    /// <param name="value">Valor configurado.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveLimitIsRejected(long value)
    {
        ValidateOptionsResult result = new LimitsOptionsValidator().Validate(
            null,
            new LimitsOptions { MaxRequestBodyBytes = value });

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Limits__MaxRequestBodyBytes", failure, StringComparison.Ordinal);
        Assert.DoesNotContain(value.ToString(System.Globalization.CultureInfo.InvariantCulture), failure, StringComparison.Ordinal);
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
