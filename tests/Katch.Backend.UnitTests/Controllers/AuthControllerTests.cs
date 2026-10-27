// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Security.Claims;
using Katch.Backend.Controllers;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Testes do <see cref="AuthController"/> que os testes de integração não alcançam: a credencial sem identificador de
/// conta nunca chega ao serviço.
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

    private sealed class RecordingAuthService : IAuthService
    {
        public int ChangePasswordCalls { get; private set; }

        public Task<LoginResponse> RegisterCandidateAsync(RegisterCandidateRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<LoginResponse> LoginAsync(LoginRequest request, ClientApp app, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
        {
            this.ChangePasswordCalls++;
            return Task.CompletedTask;
        }
    }
}
