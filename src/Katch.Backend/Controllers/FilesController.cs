// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.Controllers;

/// <summary>
/// Obtenção dos ficheiros guardados pela API (documentação da API v02, secção 6.9; modelo de classes do backend v02,
/// secção 6; RNF007). Aceita qualquer conta com sessão iniciada; a autorização por tipo de ficheiro é do serviço.
/// </summary>
[ApiController]
[Route("api/files")]
[Authorize]
public sealed class FilesController : ControllerBase
{
    private readonly IFileAccessService fileAccessService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesController"/> class.
    /// </summary>
    /// <param name="fileAccessService">Serviço de obtenção dos ficheiros.</param>
    public FilesController(IFileAccessService fileAccessService)
    {
        this.fileAccessService = fileAccessService;
    }

    /// <summary>
    /// Obtém um ficheiro (6.9.1). Um tipo desconhecido, o curriculum vitae, um nome inválido ou inexistente e a falta de
    /// autorização dão todos a mesma resposta <c>404</c> (ERR-20), para não revelar a existência do ficheiro.
    /// </summary>
    /// <param name="kind">Tipo de ficheiro, por exemplo <c>PROFILE_PHOTO</c> ou <c>COMPANY_LOGO</c>.</param>
    /// <param name="fileName">Nome do ficheiro, tal como no URL devolvido pela API.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns><c>200</c> com o ficheiro e o seu tipo de conteúdo, ou <c>404</c> (ERR-20).</returns>
    [HttpGet("{kind}/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorDto>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string kind, string fileName, CancellationToken cancellationToken)
    {
        // O tipo é lido à mão: um valor fora da enumeração não pode dar 400, tem de dar o mesmo 404.
        if (!FileRules.TryParseRouteName(kind, out FileKind fileKind))
        {
            throw new ElementNotFoundException();
        }

        FileContent file = await this.fileAccessService.GetAsync(
            this.GetAccountId(),
            this.GetUserType(),
            fileKind,
            fileName,
            cancellationToken);
        return this.File(file.Content, file.ContentType);
    }

    private Guid GetAccountId()
    {
        return this.User.TryGetAccountId(out Guid accountId)
            ? accountId
            : throw new InvalidOperationException("A credencial de sessão não traz o identificador da conta.");
    }

    private UserType GetUserType()
    {
        if (this.User.IsInRole(AccountRoles.Candidate))
        {
            return UserType.Candidate;
        }

        return this.User.IsInRole(AccountRoles.Recruiter) ? UserType.Recruiter : UserType.Admin;
    }
}
