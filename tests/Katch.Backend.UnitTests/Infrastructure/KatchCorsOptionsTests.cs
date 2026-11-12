// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Katch.Backend.UnitTests.Infrastructure;

/// <summary>
/// Testes da configuração das origens do CORS (<c>Cors:AllowedOrigins</c>, I130) e da política registada.
/// </summary>
public sealed class KatchCorsOptionsTests
{
    /// <summary>
    /// As origens separadas por vírgulas são lidas sem espaços nem entradas vazias.
    /// </summary>
    [Fact]
    public void OriginsAreSplitByCommas()
    {
        var options = new KatchCorsOptions { AllowedOrigins = " http://localhost:5173 ,, http://localhost:4173," };

        Assert.Equal(["http://localhost:5173", "http://localhost:4173"], options.ParseOrigins());
    }

    /// <summary>
    /// Sem configuração não há origens, e isso é válido: nenhum pedido de outra origem é autorizado.
    /// </summary>
    /// <param name="value">Valor configurado.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    public void MissingOriginsAreValidAndEmpty(string? value)
    {
        var options = new KatchCorsOptions { AllowedOrigins = value };

        Assert.Empty(options.ParseOrigins());
        Assert.True(new KatchCorsOptionsValidator().Validate(null, options).Succeeded);
    }

    /// <summary>
    /// Origens http e https exatas, com ou sem porto e com a barra final opcional, são aceites.
    /// </summary>
    /// <param name="value">Valor configurado.</param>
    [Theory]
    [InlineData("http://localhost:4173")]
    [InlineData("http://localhost:5173/")]
    [InlineData("https://katch.exemplo.pt")]
    [InlineData("http://127.0.0.1:4173,https://katch.exemplo.pt:8443")]
    public void ExactOriginsAreValid(string value)
    {
        Assert.True(new KatchCorsOptionsValidator().Validate(null, new KatchCorsOptions { AllowedOrigins = value }).Succeeded);
    }

    /// <summary>
    /// «*», padrões, caminhos, consultas, fragmentos, credenciais, outros esquemas e valores relativos são rejeitados
    /// com uma mensagem que indica a variável e a posição, sem repetir o valor lido.
    /// </summary>
    /// <param name="value">Valor configurado.</param>
    [Theory]
    [InlineData("*")]
    [InlineData("https://*.exemplo.pt")]
    [InlineData("http://localhost:4173/api")]
    [InlineData("http://localhost:4173?a=1")]
    [InlineData("http://localhost:4173#x")]
    [InlineData("http://utilizador@localhost:4173")]
    [InlineData("ftp://localhost")]
    [InlineData("localhost:4173")]
    [InlineData("null")]
    public void InvalidOriginsAreRejected(string value)
    {
        ValidateOptionsResult result = ValidateSecond(value);

        Assert.True(result.Failed);
        string failure = Assert.Single(result.Failures);
        Assert.Contains("origem n.º 2", failure, StringComparison.Ordinal);
        Assert.Contains("Cors__AllowedOrigins", failure, StringComparison.Ordinal);

        // A mensagem é a mesma para qualquer valor inválido, por isso não depende do valor lido.
        Assert.Equal(ValidateSecond("ftp://outro.exemplo.pt").Failures, result.Failures);
    }

    /// <summary>
    /// Opções em falta são um erro de programação.
    /// </summary>
    [Fact]
    public void NullOptionsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new KatchCorsOptionsValidator().Validate(null, null!));
    }

    /// <summary>
    /// A política registada usa as origens configuradas, normalizadas como o browser as envia, os métodos e os
    /// cabeçalhos da API, expõe o <c>Location</c> e não autoriza credenciais por cookie.
    /// </summary>
    [Fact]
    public void RegisteredPolicyUsesTheConfiguredOrigins()
    {
        using ServiceProvider provider = BuildProvider("HTTP://LocalHost:4173/, https://katch.exemplo.pt:443");

        CorsPolicy policy = provider.GetRequiredService<IOptions<CorsOptions>>().Value
            .GetPolicy(KatchCorsServiceCollectionExtensions.PolicyName)!;

        Assert.Equal(["http://localhost:4173", "https://katch.exemplo.pt"], policy.Origins);
        Assert.False(policy.AllowAnyOrigin);
        Assert.Equal(["GET", "POST", "PUT", "DELETE"], policy.Methods);
        Assert.Equal(["Authorization", "Content-Type", "X-Client-App"], policy.Headers);
        Assert.Equal(["Location"], policy.ExposedHeaders);
        Assert.False(policy.SupportsCredentials);
    }

    /// <summary>
    /// Uma origem inválida na configuração é recusada pela validação registada, que também corre no arranque.
    /// </summary>
    [Fact]
    public void RegistrationRejectsAWildcard()
    {
        using ServiceProvider provider = BuildProvider("*");

        OptionsValidationException error = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<KatchCorsOptions>>().Value);

        Assert.Contains("Cors__AllowedOrigins", error.Message, StringComparison.Ordinal);
    }

    private static ValidateOptionsResult ValidateSecond(string value) =>
        new KatchCorsOptionsValidator().Validate(null, new KatchCorsOptions { AllowedOrigins = "http://localhost:5173," + value });

    private static ServiceProvider BuildProvider(string origins)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins"] = origins })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddKatchCors();
        return services.BuildServiceProvider();
    }
}
