// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Security.Claims;
using Katch.Backend.Controllers;
using Katch.Backend.Controllers.Mapping;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;
using Katch.Backend.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Testes do <see cref="AuthController"/> (documentação da API v02, secção 6.1): a credencial sem identificador de conta
/// nunca chega ao serviço, os registos e o login devolvem os códigos de estado documentados e o cabeçalho
/// <c>X-Client-App</c> é validado no controller. As regras de validação dos registos são verificadas nos testes do
/// <see cref="AuthService"/>.
/// </summary>
public sealed class AuthControllerTests
{
    /// <summary>
    /// Uma credencial sem a claim <c>sub</c> é recusada com o ERR-01 e a palavra-passe não é alterada (falha fechada).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ChangePasswordWithoutAccountIdReturnsErr01AndDoesNotCallTheService()
    {
        var service = new RecordingAuthService();
        var controller = new AuthController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("Bearer")) },
            },
        };

        IActionResult result = await controller.ChangePassword(
            new ChangePasswordRequest("katch2026", "nova2026"),
            TestContext.Current.CancellationToken);

        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        ApiErrorDto body = Assert.IsType<ApiErrorDto>(unauthorized.Value);
        Assert.Equal("ERR-01", body.Code);
        Assert.Equal(0, service.ChangePasswordCalls);
    }

    /// <summary>
    /// O registo do Candidato devolve <c>201</c> com a sessão e a rota do perfil em <c>Location</c>; o da conta do
    /// Recrutador devolve <c>201</c> sem <c>Location</c>. Sem corpo, o serviço recebe o pedido vazio e é ele que indica
    /// todos os campos obrigatórios (6.1.1, 6.1.2; RF003, RF037).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RegistrationsReturnCreatedAndPassEmptyRequestsToTheService()
    {
        var service = new ServiceDouble<IAuthService>();
        LoginResult session = ServiceModels.Session;
        service
            .Returns(nameof(IAuthService.RegisterCandidateAsync), session)
            .Returns(nameof(IAuthService.CreateRecruiterAccountAsync), session);
        AuthController controller = new AuthController(service.Service).WithUser(null);

        ActionResult<LoginResponse> candidate = await controller.RegisterCandidate(null, TestContext.Current.CancellationToken);
        ActionResult<LoginResponse> recruiter = await controller.CreateRecruiterAccount(null, TestContext.Current.CancellationToken);

        CreatedResult created = Assert.IsType<CreatedResult>(candidate.Result);
        Assert.Equal("/api/candidate/profile", created.Location);
        Assert.Equal(session.ToDto(), created.Value);
        ObjectResult recruiterCreated = Assert.IsType<ObjectResult>(recruiter.Result);
        Assert.Equal(StatusCodes.Status201Created, recruiterCreated.StatusCode);
        Assert.Equal(session.ToDto(), recruiterCreated.Value);
        Assert.Equal(RegisterCandidateInput.Empty, service.Calls[0].Arguments[0]);
        Assert.Equal(CreateRecruiterAccountInput.Empty, service.Calls[1].Arguments[0]);
    }

    /// <summary>
    /// O cabeçalho <c>X-Client-App</c> identifica o ponto de acesso: <c>mobile</c> e <c>web</c> chegam ao serviço como
    /// <see cref="ClientApp"/> e o login devolve <c>200</c> com a sessão (6.1.3; RF001, RF038, RF083).
    /// </summary>
    /// <param name="header">Valor do cabeçalho.</param>
    /// <param name="expected">Ponto de acesso esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("mobile", ClientApp.Mobile)]
    [InlineData("web", ClientApp.Web)]
    public async Task LoginReadsTheClientApp(string header, ClientApp expected)
    {
        var service = new ServiceDouble<IAuthService>();
        LoginResult session = ServiceModels.Session;
        service.Returns(nameof(IAuthService.LoginAsync), session);
        AuthController controller = new AuthController(service.Service).WithUser(null);

        ActionResult<LoginResponse> result = await controller.Login(header, null, TestContext.Current.CancellationToken);

        Assert.Equal(session.ToDto(), Assert.IsType<OkObjectResult>(result.Result).Value);
        ServiceCall call = service.SingleCall(nameof(IAuthService.LoginAsync));
        Assert.Equal(LoginInput.Empty, call.Arguments[0]);
        Assert.Equal(expected, call.Arguments[1]);
    }

    /// <summary>
    /// Sem o cabeçalho <c>X-Client-App</c> o campo é obrigatório; com outro valor (incluindo maiúsculas) é inválido. Em
    /// ambos os casos dá o ERR-10 com o cabeçalho como campo e o serviço não é chamado.
    /// </summary>
    /// <param name="header">Valor do cabeçalho.</param>
    /// <param name="required">Se o erro esperado é o de campo obrigatório.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null, true)]
    [InlineData("  ", true)]
    [InlineData("Web", false)]
    [InlineData("desktop", false)]
    public async Task LoginRejectsAMissingOrUnknownClientApp(string? header, bool required)
    {
        var service = new ServiceDouble<IAuthService>();
        AuthController controller = new AuthController(service.Service).WithUser(null);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => controller.Login(header, new LoginRequest("ana@exemplo.pt", "katch2026"), TestContext.Current.CancellationToken));

        Assert.Equal(
            new FieldError("X-Client-App", required ? FieldReasons.Required : FieldReasons.InvalidValue),
            Assert.Single(exception.Fields));
        Assert.Empty(service.Calls);
    }

    /// <summary>
    /// O logout devolve <c>204</c> (o token é descartado pelo cliente; 6.1.4) e a alteração da palavra-passe usa a conta
    /// da credencial, nunca a do pedido (6.1.5).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LogoutAndChangePassword()
    {
        var service = new ServiceDouble<IAuthService>();
        Guid userId = Guid.NewGuid();
        AuthController controller = new AuthController(service.Service).WithUser(userId, AccountRoles.Candidate);

        IActionResult logout = controller.Logout();
        IActionResult changed = await controller.ChangePassword(null, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(logout);
        Assert.IsType<NoContentResult>(changed);
        ServiceCall call = service.SingleCall(nameof(IAuthService.ChangePasswordAsync));
        Assert.Equal(userId, call.Arguments[0]);
        Assert.Equal(ChangePasswordInput.Empty, call.Arguments[1]);
    }

    private sealed class RecordingAuthService : IAuthService
    {
        public int ChangePasswordCalls { get; private set; }

        public Task<LoginResult> RegisterCandidateAsync(RegisterCandidateInput request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<LoginResult> CreateRecruiterAccountAsync(CreateRecruiterAccountInput request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<LoginResult> LoginAsync(LoginInput request, ClientApp app, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task ChangePasswordAsync(Guid userId, ChangePasswordInput request, CancellationToken cancellationToken = default)
        {
            this.ChangePasswordCalls++;
            return Task.CompletedTask;
        }
    }
}
