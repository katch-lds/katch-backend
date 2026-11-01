// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Controllers;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Testes do <see cref="FilesController"/> (obtenção de ficheiros, documentação da API v02, secção 6.9) e do
/// <see cref="ReferenceListsController"/> (listas pré-definidas e gestão das competências e dos benefícios, secção
/// 6.13) com os serviços substituídos por duplos. A validação das designações das competências é
/// verificada nos testes do <see cref="ReferenceListService"/>.
/// </summary>
public sealed class FilesAndReferenceListsControllersTests
{
    private readonly Guid accountId = Guid.NewGuid();

    /// <summary>
    /// O tipo de ficheiro da rota é reconhecido só na forma exata e o tipo de conta vem do papel da credencial.
    /// </summary>
    /// <param name="kind">Tipo na rota.</param>
    /// <param name="role">Papel da credencial.</param>
    /// <param name="expectedKind">Tipo de ficheiro esperado.</param>
    /// <param name="expectedUserType">Tipo de conta esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("PROFILE_PHOTO", AccountRoles.Candidate, FileKind.ProfilePhoto, UserType.Candidate)]
    [InlineData("CURRICULUM_VITAE", AccountRoles.Recruiter, FileKind.CurriculumVitae, UserType.Recruiter)]
    [InlineData("JOB_PHOTO", AccountRoles.Admin, FileKind.JobPhoto, UserType.Admin)]
    public async Task FileKindAndAccountTypeAreReadFromTheRouteAndTheCredential(
        string kind,
        string role,
        FileKind expectedKind,
        UserType expectedUserType)
    {
        var service = new ServiceDouble<IFileAccessService>();
        using var content = new MemoryStream([1, 2, 3]);
        service.Returns(nameof(IFileAccessService.GetAsync), new FileContent(content, "image/png", "a.png"));
        FilesController controller = new FilesController(service.Service).WithUser(this.accountId, role);

        IActionResult result = await controller.Get(kind, "a.png", TestContext.Current.CancellationToken);

        Assert.Equal("image/png", Assert.IsType<FileStreamResult>(result).ContentType);
        ServiceCall call = service.SingleCall(nameof(IFileAccessService.GetAsync));
        Assert.Equal(this.accountId, call.Arguments[0]);
        Assert.Equal(expectedUserType, call.Arguments[1]);
        Assert.Equal(expectedKind, call.Arguments[2]);
        Assert.Equal("a.png", call.Arguments[3]);
    }

    /// <summary>
    /// Um tipo desconhecido ou noutra forma dá o mesmo ERR-20 de um ficheiro inexistente, sem chamar o serviço.
    /// </summary>
    /// <param name="kind">Tipo na rota.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData("profile_photo")]
    [InlineData("ProfilePhoto")]
    [InlineData("OUTRO")]
    public async Task UnknownFileKindIsNotFound(string kind)
    {
        var service = new ServiceDouble<IFileAccessService>();
        FilesController controller = new FilesController(service.Service).WithUser(this.accountId, AccountRoles.Candidate);

        await Assert.ThrowsAsync<ElementNotFoundException>(() => controller.Get(kind, "a.png", TestContext.Current.CancellationToken));

        Assert.Empty(service.Calls);
    }

    /// <summary>
    /// As listas de localidades, competências e benefícios devolvem <c>200</c> com o resultado do serviço (6.13.1 a 6.13.3).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListsReturnTheServiceResult()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var service = new ServiceDouble<IReferenceListService>();
        List<ReferenceItemDto> locations = [];
        List<ReferenceItemDto> skills = [];
        List<ReferenceItemDto> benefits = [];
        service
            .Returns(nameof(IReferenceListService.ListLocationsAsync), locations)
            .Returns(nameof(IReferenceListService.ListSkillsAsync), skills)
            .Returns(nameof(IReferenceListService.ListBenefitsAsync), benefits);
        var controller = new ReferenceListsController(service.Service);

        Assert.Same(locations, Assert.IsType<OkObjectResult>((await controller.ListLocations(token)).Result).Value);
        Assert.Same(skills, Assert.IsType<OkObjectResult>((await controller.ListSkills(token)).Result).Value);
        Assert.Same(benefits, Assert.IsType<OkObjectResult>((await controller.ListBenefits(token)).Result).Value);
    }

    /// <summary>
    /// A criação de uma competência ou de um benefício devolve <c>201</c>; a alteração da designação devolve o item; o
    /// Administrador é o da credencial e, sem corpo, o serviço recebe o pedido vazio, que exige a designação (6.13.4 a
    /// 6.13.7; RF099 a RF102).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AdminWritesOnTheLists()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var service = new ServiceDouble<IReferenceListService>();
        var item = new ReferenceItemDto(Guid.NewGuid(), "C#");
        foreach (string method in new[]
        {
            nameof(IReferenceListService.AddSkillAsync),
            nameof(IReferenceListService.RenameSkillAsync),
            nameof(IReferenceListService.AddBenefitAsync),
            nameof(IReferenceListService.RenameBenefitAsync),
        })
        {
            service.Returns(method, item);
        }

        ReferenceListsController controller = new ReferenceListsController(service.Service).WithUser(this.accountId, AccountRoles.Admin);
        Guid itemId = Guid.NewGuid();

        ObjectResult skill = Assert.IsType<ObjectResult>((await controller.AddSkill(null, token)).Result);
        ActionResult<ReferenceItemDto> renamedSkill = await controller.RenameSkill(itemId, new ReferenceItemRequest("C# 14"), token);
        ObjectResult benefit = Assert.IsType<ObjectResult>((await controller.AddBenefit(null, token)).Result);
        ActionResult<ReferenceItemDto> renamedBenefit = await controller.RenameBenefit(itemId, null, token);

        Assert.Equal(StatusCodes.Status201Created, skill.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, benefit.StatusCode);
        Assert.Same(item, renamedSkill.Value);
        Assert.Same(item, renamedBenefit.Value);
        Assert.All(service.Calls, call => Assert.Equal(this.accountId, call.Arguments[0]));
        Assert.Same(ReferenceItemRequest.Empty, service.Calls[0].Arguments[1]);
        Assert.Equal(itemId, service.Calls[1].Arguments[1]);
        Assert.Equal("C# 14", service.Calls[1].Argument<ReferenceItemRequest>(2).Name);
        Assert.Same(ReferenceItemRequest.Empty, service.Calls[3].Arguments[2]);
    }

    /// <summary>
    /// Uma credencial sem o identificador da conta nunca chega aos serviços.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CredentialWithoutAccountIdNeverReachesTheServices()
    {
        var files = new ServiceDouble<IFileAccessService>();
        var lists = new ServiceDouble<IReferenceListService>();
        CancellationToken token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new FilesController(files.Service).WithUser(null, AccountRoles.Candidate).Get("PROFILE_PHOTO", "a.png", token));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ReferenceListsController(lists.Service).WithUser(null, AccountRoles.Admin).AddSkill(null, token));

        Assert.Empty(files.Calls);
        Assert.Empty(lists.Calls);
    }
}
