// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Auxiliares dos testes dos controllers: contexto HTTP com a credencial de sessão (claims <c>sub</c> e <c>role</c>,
/// como as do token JWT da aplicação) e formulário <c>multipart/form-data</c> já lido.
/// </summary>
public static class ControllerTesting
{
    /// <summary>
    /// Nome do campo do ficheiro nos carregamentos (documentação da API v02, secção 2.6).
    /// </summary>
    public const string UploadField = "file";

    /// <summary>
    /// Cria uma instância do tipo sem correr o construtor, para servir de resultado do serviço: o teste verifica que o
    /// controller devolve esse mesmo objeto (<see cref="Assert.Same"/>), sem depender do conteúdo do DTO.
    /// </summary>
    /// <typeparam name="T">Tipo do resultado.</typeparam>
    /// <returns>A instância.</returns>
    public static T Sentinel<T>()
        where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    /// <summary>
    /// Associa ao controller um pedido autenticado com a conta e o tipo de conta indicados.
    /// </summary>
    /// <typeparam name="TController">Tipo do controller.</typeparam>
    /// <param name="controller">Controller em teste.</param>
    /// <param name="accountId">Identificador da conta (claim <c>sub</c>); <see langword="null"/> omite a claim.</param>
    /// <param name="role">Tipo de conta (claim <c>role</c>); <see langword="null"/> omite a claim.</param>
    /// <returns>O próprio controller.</returns>
    public static TController WithUser<TController>(this TController controller, Guid? accountId, string? role = null)
        where TController : ControllerBase
    {
        ArgumentNullException.ThrowIfNull(controller);

        var claims = new List<Claim>();
        if (accountId is not null)
        {
            claims.Add(new Claim("sub", accountId.Value.ToString()));
        }

        if (role is not null)
        {
            claims.Add(new Claim("role", role));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer", "sub", "role")),
            },
        };
        return controller;
    }

    /// <summary>
    /// Coloca no pedido do controller um formulário <c>multipart/form-data</c> já lido, com um ficheiro no campo
    /// indicado, ou sem ficheiros.
    /// </summary>
    /// <typeparam name="TController">Tipo do controller.</typeparam>
    /// <param name="controller">Controller com o contexto já definido por <see cref="WithUser"/>.</param>
    /// <param name="field">Campo do ficheiro; <see langword="null"/> envia o formulário sem ficheiros.</param>
    /// <returns>O ficheiro enviado, ou <see langword="null"/>.</returns>
    public static IFormFile? WithUpload<TController>(this TController controller, string? field = UploadField)
        where TController : ControllerBase
    {
        ArgumentNullException.ThrowIfNull(controller);

        var files = new FormFileCollection();
        FormFile? file = null;
        if (field is not null)
        {
            byte[] content = [0x89, 0x50, 0x4E, 0x47];
            file = new FormFile(new MemoryStream(content), 0, content.Length, field, "foto.png");
            files.Add(file);
        }

        HttpRequest request = controller.HttpContext.Request;
        request.ContentType = "multipart/form-data; boundary=katch";
        request.Form = new FormCollection(new Dictionary<string, StringValues>(), files);
        return file;
    }
}
