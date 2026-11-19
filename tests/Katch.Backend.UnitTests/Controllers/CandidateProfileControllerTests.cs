// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Controllers;
using Katch.Backend.Controllers.Mapping;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Services;
using Katch.Backend.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Testes do <see cref="CandidateProfileController"/> (documentação da API v02, secções 6.2.1 a 6.2.11) com o serviço
/// substituído por um duplo: o Candidato é sempre o da credencial, os pedidos sem corpo chegam ao serviço como pedidos
/// vazios (para que o serviço indique os campos obrigatórios), os modelos devolvidos pelo serviço são convertidos no
/// formato da API (I138) e cada operação devolve o código de estado documentado.
/// As regras do perfil e das competências são verificadas nos testes do <see cref="CandidateProfileService"/>.
/// </summary>
public sealed class CandidateProfileControllerTests
{
    private readonly Guid candidateId = Guid.NewGuid();
    private readonly ServiceDouble<ICandidateProfileService> service = new();
    private readonly CandidateProfileController controller;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateProfileControllerTests"/> class.
    /// </summary>
    public CandidateProfileControllerTests()
    {
        this.controller = new CandidateProfileController(this.service.Service).WithUser(this.candidateId, AccountRoles.Candidate);
    }

    /// <summary>
    /// A consulta devolve o perfil do Candidato da credencial (6.2.1).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetReturnsTheProfileOfTheSessionCandidate()
    {
        CandidateProfileModel profile = ServiceModels.CandidateProfile;
        this.service.Returns(nameof(ICandidateProfileService.GetAsync), profile);

        ActionResult<CandidateProfileDto> result = await this.controller.Get(TestContext.Current.CancellationToken);

        Assert.Equivalent(profile.ToDto(), result.Value, strict: true);
        Assert.Equal(this.candidateId, this.service.SingleCall(nameof(ICandidateProfileService.GetAsync)).Arguments[0]);
    }

    /// <summary>
    /// Sem corpo, as operações de escrita chegam ao serviço com o pedido vazio, que indica os campos obrigatórios (ERR-10)
    /// em vez de um <c>400</c> sem campos.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task MissingBodiesReachTheServiceAsEmptyRequests()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        this.ReturnProfiles();

        await this.controller.Update(null, token);
        await this.controller.UpdatePreferences(null, token);
        await this.controller.SetLinks(null, token);
        await this.controller.AddSkill(null, token);
        await this.controller.AddCustomSkill(null, token);

        Assert.Equal(
            [
                nameof(ICandidateProfileService.UpdateAsync),
                nameof(ICandidateProfileService.UpdatePreferencesAsync),
                nameof(ICandidateProfileService.SetLinksAsync),
                nameof(ICandidateProfileService.AddSkillAsync),
                nameof(ICandidateProfileService.AddCustomSkillAsync),
            ],
            this.service.Calls.Select(call => call.Method));
        Assert.All(this.service.Calls, call => Assert.Equal(this.candidateId, call.Arguments[0]));
        Assert.Equal(UpdateCandidateProfileInput.Empty, this.service.Calls[0].Arguments[1]);
        Assert.Equal(UpdatePreferencesRequest.Empty.ToPreferences(), this.service.Calls[1].Arguments[1]);
        Assert.Equal(SetLinksInput.Empty, this.service.Calls[2].Arguments[1]);
        Assert.Equal(AddSkillInput.Empty, this.service.Calls[3].Arguments[1]);
        Assert.Equal(AddCustomSkillInput.Empty, this.service.Calls[4].Arguments[1]);
    }

    /// <summary>
    /// A alteração do perfil, das preferências e das hiperligações passa os dados do pedido ao serviço e devolve
    /// <c>200</c> com o resultado no formato da API (6.2.2, 6.2.3, 6.2.6).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task WritesPassTheRequestAndReturnTheResult()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        CandidateProfileModel profile = ServiceModels.CandidateProfile;
        SearchPreferencesModel preferences = ServiceModels.Preferences;
        this.service
            .Returns(nameof(ICandidateProfileService.UpdateAsync), profile)
            .Returns(nameof(ICandidateProfileService.UpdatePreferencesAsync), preferences)
            .Returns(nameof(ICandidateProfileService.SetLinksAsync), profile);
        var update = new UpdateCandidateProfileRequest("Programadora", Guid.NewGuid(), null, null);
        var links = new SetLinksRequest(["https://a.pt"]);

        Assert.Equivalent(profile.ToDto(), (await this.controller.Update(update, token)).Value, strict: true);
        Assert.Equivalent(
            preferences.ToDto(),
            (await this.controller.UpdatePreferences(UpdatePreferencesRequest.Empty, token)).Value,
            strict: true);
        Assert.Equivalent(profile.ToDto(), (await this.controller.SetLinks(links, token)).Value, strict: true);

        Assert.Equal(update.ToInput(), this.service.Calls[0].Arguments[1]);
        Assert.Equal(links.ToInput(), this.service.Calls[2].Arguments[1]);
    }

    /// <summary>
    /// A fotografia guardada dá <c>204</c> com o URL no cabeçalho <c>Location</c>; o ficheiro lido do campo
    /// <c>file</c> é o que chega ao serviço (6.2.7).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UploadPhotoReturnsNoContentWithTheLocation()
    {
        IFormFile? file = this.controller.WithUpload();
        this.service.Returns(nameof(ICandidateProfileService.UploadPhotoAsync), "/api/files/PROFILE_PHOTO/a.png");

        IActionResult result = await this.controller.UploadPhoto(TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("/api/files/PROFILE_PHOTO/a.png", this.controller.Response.Headers[HeaderNames.Location]);
        ServiceCall call = this.service.SingleCall(nameof(ICandidateProfileService.UploadPhotoAsync));
        Assert.Equal(this.candidateId, call.Arguments[0]);
        Assert.Same(file, call.Arguments[1]);
    }

    /// <summary>
    /// O curriculum vitae guardado dá <c>204</c> sem <c>Location</c>; sem o campo <c>file</c> o serviço recebe
    /// <see langword="null"/> e é ele que responde com o ERR-11 (6.2.8).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UploadCvWithoutTheFileFieldPassesNullToTheService()
    {
        this.controller.WithUpload(field: "outro");

        IActionResult result = await this.controller.UploadCv(TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.False(this.controller.Response.Headers.ContainsKey(HeaderNames.Location));
        Assert.Null(this.service.SingleCall(nameof(ICandidateProfileService.UploadCvAsync)).Arguments[1]);
    }

    /// <summary>
    /// A obtenção do curriculum vitae devolve o conteúdo com o tipo do ficheiro (6.2.9).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task GetCvReturnsTheFile()
    {
        using var content = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        this.service.Returns(nameof(ICandidateProfileService.GetCvAsync), new FileContent(content, "application/pdf", "cv.pdf"));

        IActionResult result = await this.controller.GetCv(TestContext.Current.CancellationToken);

        FileStreamResult file = Assert.IsType<FileStreamResult>(result);
        Assert.Same(content, file.FileStream);
        Assert.Equal("application/pdf", file.ContentType);
    }

    /// <summary>
    /// A associação de uma competência da lista e de uma competência «Outro» devolve <c>201</c> com o perfil; a remoção
    /// devolve <c>204</c> e passa o identificador da rota (RF008, RF009).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SkillOperationsReturnCreatedOrNoContent()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        CandidateProfileModel profile = ServiceModels.CandidateProfile;
        this.service
            .Returns(nameof(ICandidateProfileService.AddSkillAsync), profile)
            .Returns(nameof(ICandidateProfileService.AddCustomSkillAsync), profile);
        Guid skillId = Guid.NewGuid();
        Guid customSkillId = Guid.NewGuid();

        ActionResult<CandidateProfileDto> added = await this.controller.AddSkill(new AddSkillRequest(skillId), token);
        ActionResult<CandidateProfileDto> custom = await this.controller.AddCustomSkill(new AddCustomSkillRequest("Kotlin"), token);
        IActionResult removed = await this.controller.RemoveSkill(skillId, token);
        IActionResult removedCustom = await this.controller.RemoveCustomSkill(customSkillId, token);

        ObjectResult addedResult = Assert.IsType<ObjectResult>(added.Result);
        Assert.Equal(StatusCodes.Status201Created, addedResult.StatusCode);
        Assert.Equivalent(profile.ToDto(), addedResult.Value, strict: true);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(custom.Result).StatusCode);
        Assert.IsType<NoContentResult>(removed);
        Assert.IsType<NoContentResult>(removedCustom);
        Assert.Equal(skillId, this.service.Calls[2].Arguments[1]);
        Assert.Equal(customSkillId, this.service.Calls[3].Arguments[1]);
    }

    /// <summary>
    /// Uma credencial sem a claim <c>sub</c> nunca chega ao serviço (falha fechada; a autenticação já a teria recusado).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CredentialWithoutAccountIdNeverReachesTheService()
    {
        this.controller.WithUser(null, AccountRoles.Candidate);

        await Assert.ThrowsAsync<InvalidOperationException>(() => this.controller.Get(TestContext.Current.CancellationToken));

        Assert.Empty(this.service.Calls);
    }

    private void ReturnProfiles()
    {
        this.service
            .Returns(nameof(ICandidateProfileService.UpdateAsync), ServiceModels.CandidateProfile)
            .Returns(nameof(ICandidateProfileService.UpdatePreferencesAsync), ServiceModels.Preferences)
            .Returns(nameof(ICandidateProfileService.SetLinksAsync), ServiceModels.CandidateProfile)
            .Returns(nameof(ICandidateProfileService.AddSkillAsync), ServiceModels.CandidateProfile)
            .Returns(nameof(ICandidateProfileService.AddCustomSkillAsync), ServiceModels.CandidateProfile);
    }
}
