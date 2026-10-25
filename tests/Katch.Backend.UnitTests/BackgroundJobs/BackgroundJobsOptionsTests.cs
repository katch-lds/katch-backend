// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Katch.Backend.UnitTests.BackgroundJobs;

/// <summary>
/// Testes da configuração das tarefas periódicas (<c>BackgroundJobs:IntervalSeconds</c>, 60 segundos por omissão) e do
/// seu registo (modelo de classes do backend v02, secção 5.2).
/// </summary>
public sealed class BackgroundJobsOptionsTests
{
    /// <summary>
    /// Sem configuração o intervalo é de 60 segundos, a cada minuto.
    /// </summary>
    [Fact]
    public void DefaultIntervalIsOneMinute()
    {
        Assert.Equal(60, new BackgroundJobsOptions().IntervalSeconds);
        Assert.Equal(60, BackgroundJobsOptions.DefaultIntervalSeconds);
    }

    /// <summary>
    /// Os valores de 1 segundo a 24 horas são aceites.
    /// </summary>
    /// <param name="seconds">Intervalo em segundos.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(3600)]
    [InlineData(86_400)]
    public void IntervalWithinTheLimitsIsValid(int seconds)
    {
        ValidateOptionsResult result = Validate(seconds);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Zero, negativos e valores acima de 24 horas são rejeitados, com uma mensagem que diz como corrigir e não repete o valor.
    /// </summary>
    /// <param name="seconds">Intervalo em segundos.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(86_401)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void IntervalOutsideTheLimitsIsRejected(int seconds)
    {
        ValidateOptionsResult result = Validate(seconds);

        Assert.True(result.Failed);
        string failure = Assert.Single(result.Failures!);
        Assert.Contains("BackgroundJobs__IntervalSeconds", failure, StringComparison.Ordinal);
        Assert.Contains("86400", failure, StringComparison.Ordinal);

        // O valor lido não é repetido na mensagem (os valores pequenos coincidem com os limites e com o valor por omissão).
        if (Math.Abs((long)seconds) > 1000)
        {
            Assert.DoesNotContain(seconds.ToString(CultureInfo.InvariantCulture), failure, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Opções em falta são um erro de programação.
    /// </summary>
    [Fact]
    public void NullOptionsAreRejected()
    {
        var validator = new BackgroundJobsOptionsValidator();

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null, null!));
    }

    /// <summary>
    /// O intervalo é lido da configuração e a tarefa de reposição fica registada como serviço de fundo.
    /// </summary>
    [Fact]
    public void RegistrationBindsTheConfigurationAndAddsTheQuotaRestoreWorker()
    {
        using ServiceProvider provider = BuildProvider("5");

        Assert.Equal(5, provider.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value.IntervalSeconds);
        Assert.Contains(provider.GetServices<IHostedService>(), service => service is QuotaRestoreWorker);
    }

    /// <summary>
    /// Sem valor na configuração fica o intervalo por omissão.
    /// </summary>
    [Fact]
    public void RegistrationUsesTheDefaultIntervalWhenNothingIsConfigured()
    {
        using ServiceProvider provider = BuildProvider(null);

        Assert.Equal(60, provider.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value.IntervalSeconds);
    }

    /// <summary>
    /// Um intervalo inválido na configuração é recusado pela validação registada, que também corre no arranque.
    /// </summary>
    [Fact]
    public void RegistrationRejectsAnInvalidConfiguredInterval()
    {
        using ServiceProvider provider = BuildProvider("0");

        OptionsValidationException error = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value);

        Assert.Contains("BackgroundJobs__IntervalSeconds", error.Message, StringComparison.Ordinal);
    }

    private static ValidateOptionsResult Validate(int seconds)
    {
        var validator = new BackgroundJobsOptionsValidator();
        return validator.Validate(null, new BackgroundJobsOptions { IntervalSeconds = seconds });
    }

    private static ServiceProvider BuildProvider(string? intervalSeconds)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                intervalSeconds is null
                    ? []
                    : new Dictionary<string, string?> { ["BackgroundJobs:IntervalSeconds"] = intervalSeconds })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddKatchBackgroundJobs();
        return services.BuildServiceProvider();
    }
}
