// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Reflection;
using Katch.Backend.Services;

namespace Katch.Backend.UnitTests.Architecture;

/// <summary>
/// Testes de arquitetura da camada de serviços (documentação da arquitetura v02, secção 2.6; I138): os DTO da API
/// (pedidos e respostas, namespace <c>Katch.Backend.Dtos</c>) só existem na camada dos controllers. Nenhum ficheiro da
/// pasta <c>Services</c> referencia o namespace dos DTO e nenhum tipo da camada de serviços recebe, devolve ou guarda
/// um DTO; a conversão é feita na camada da API (<c>Katch.Backend.Controllers.Mapping</c>).
/// </summary>
public sealed class ServicesLayerTests
{
    private const string DtosNamespace = "Katch.Backend.Dtos";

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Nenhum ficheiro de código da pasta <c>src/Katch.Backend/Services</c> (incluindo as subpastas) referencia o
    /// namespace <c>Katch.Backend.Dtos</c>, por <c>using</c> ou pelo nome completo.
    /// </summary>
    [Fact]
    public void NoServicesFileReferencesTheDtosNamespace()
    {
        string folder = Path.Combine(RepositoryRoot(), "src", "Katch.Backend", "Services");
        string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);

        string[] offenders = files
            .Where(file => File.ReadAllText(file).Contains(DtosNamespace, StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(folder, file))
            .ToArray();

        Assert.NotEmpty(files);
        Assert.Empty(offenders);
    }

    /// <summary>
    /// Nenhum tipo da camada de serviços (namespace <c>Katch.Backend.Services</c> e subnamespaces) tem um DTO nas
    /// interfaces, nos parâmetros, nos resultados, nos campos, nas propriedades nem nas variáveis locais dos métodos.
    /// </summary>
    [Fact]
    public void NoServicesTypeUsesADto()
    {
        Type[] services = typeof(IAuthService).Assembly.GetTypes()
            .Where(type => type.Namespace is not null
                && (type.Namespace == typeof(IAuthService).Namespace
                    || type.Namespace.StartsWith(typeof(IAuthService).Namespace + ".", StringComparison.Ordinal)))
            .ToArray();

        string[] offenders = services
            .SelectMany(type => UsedTypes(type).Where(IsDto).Select(used => $"{type.FullName} usa {used.FullName}"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(services.Length >= 14);
        Assert.Empty(offenders);
    }

    /// <summary>
    /// A conversão da página da Empresa está na camada da API e não na camada de serviços.
    /// </summary>
    [Fact]
    public void CompanyPageMapperIsInTheApiLayer()
    {
        Type mapper = typeof(Katch.Backend.Controllers.Mapping.CompanyPageMapper);

        Assert.Equal("Katch.Backend.Controllers.Mapping", mapper.Namespace);
        Assert.Null(typeof(IAuthService).Assembly.GetType("Katch.Backend.Services.CompanyPageMapper"));
    }

    private static bool IsDto(Type type)
    {
        return type.Namespace == DtosNamespace;
    }

    private static IEnumerable<Type> UsedTypes(Type type)
    {
        var used = new List<Type>();
        if (type.BaseType is not null)
        {
            used.Add(type.BaseType);
        }

        used.AddRange(type.GetInterfaces());
        used.AddRange(type.GetFields(AllMembers).Select(field => field.FieldType));
        used.AddRange(type.GetProperties(AllMembers).Select(property => property.PropertyType));

        foreach (MethodBase method in type.GetMethods(AllMembers).Cast<MethodBase>().Concat(type.GetConstructors(AllMembers)))
        {
            used.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
            if (method is MethodInfo info)
            {
                used.Add(info.ReturnType);
            }

            MethodBody? body = method.GetMethodBody();
            if (body is not null)
            {
                used.AddRange(body.LocalVariables.Select(local => local.LocalType));
            }
        }

        return used.SelectMany(Expand);
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        if (type.HasElementType)
        {
            return Expand(type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            return new[] { type.GetGenericTypeDefinition() }.Concat(type.GetGenericArguments().SelectMany(Expand));
        }

        return [type];
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Katch.Backend.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
