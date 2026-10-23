// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Net;
using System.Net.Http.Headers;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de <c>POST /api/auth/logout</c> sobre a API em memória (documentação da API v02, secções 3.5 e 6.1.4).
/// </summary>
public sealed class LogoutTests : IClassFixture<KatchWebApplicationFactory>
{
    private const string Route = "/api/auth/logout";

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public LogoutTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Qualquer tipo de conta termina a sessão com 204 e sem corpo (RF105 a RF107).
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate)]
    [InlineData(UserType.Recruiter)]
    [InlineData(UserType.Admin)]
    public async Task EveryAccountTypeGets204WithoutBody(UserType userType)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, userType);

        using HttpResponseMessage response = await this.PostAsync(this.IssueToken(user));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// O fim de sessão não depende da Empresa do Recrutador: com a Empresa pendente também responde 204.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RecruiterWithPendingCompanyGets204()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Recruiter, companyStatus: CompanyStatus.Pending);

        using HttpResponseMessage response = await this.PostAsync(this.IssueToken(user));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Sem credencial, ou com uma credencial inválida, o pedido é recusado com o ERR-01.
    /// </summary>
    /// <param name="token">Credencial enviada, ou <see langword="null"/> se não houver.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("abc.def.ghi")]
    [InlineData("isto-nao-e-um-jwt")]
    public async Task MissingOrInvalidTokenReturnsErr01(string? token)
    {
        using HttpResponseMessage response = await this.PostAsync(token);

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            "ERR-01",
            "A sessão expirou ou não é válida. Inicie sessão novamente.");
    }

    /// <summary>
    /// Uma credencial expirada é recusada com o ERR-01.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ExpiredTokenReturnsErr01()
    {
        AppUser user = await AuthTestAccounts.SeedAsync(this.factory, UserType.Candidate);

        using HttpResponseMessage response = await this.PostAsync(this.IssueToken(user, DateTimeOffset.UtcNow.AddHours(-9)));

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            "ERR-01",
            "A sessão expirou ou não é válida. Inicie sessão novamente.");
    }

    /// <summary>
    /// Uma conta bloqueada ou suspensa não consegue terminar a sessão: a política «qualquer conta» exige a conta ativa.
    /// </summary>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="suspended"><see langword="true"/> para suspender a conta; <see langword="false"/> para a bloquear.</param>
    /// <param name="code">Código esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(UserType.Candidate, false, "ERR-03")]
    [InlineData(UserType.Recruiter, false, "ERR-03")]
    [InlineData(UserType.Candidate, true, "ERR-04")]
    public async Task BlockedOrSuspendedAccountReturns403(UserType userType, bool suspended, string code)
    {
        AppUser user = await AuthTestAccounts.SeedAsync(
            this.factory,
            userType,
            account =>
            {
                if (suspended)
                {
                    account.Suspend(DateTimeOffset.UtcNow);
                }
                else
                {
                    account.Block(DateTimeOffset.UtcNow);
                }
            });

        using HttpResponseMessage response = await this.PostAsync(this.IssueToken(user));

        await AuthenticationTests.AssertErrorAsync(
            response,
            HttpStatusCode.Forbidden,
            code,
            suspended ? "A conta está suspensa." : "A conta está bloqueada.");
    }

    private string IssueToken(AppUser user, DateTimeOffset? issuedAt = null)
    {
        ITokenService tokens = this.factory.Services.GetRequiredService<ITokenService>();
        this.factory.Clock.Fixed = issuedAt;
        try
        {
            return tokens.CreateToken(user).Token;
        }
        finally
        {
            this.factory.Clock.Fixed = null;
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string? token)
    {
        using HttpClient client = this.factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Route, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
