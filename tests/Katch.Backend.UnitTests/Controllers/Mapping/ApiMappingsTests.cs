// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Reflection;
using Katch.Backend.Controllers.Mapping;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Services.Models;

namespace Katch.Backend.UnitTests.Controllers.Mapping;

/// <summary>
/// Testes das conversões da camada da API (I138): cada pedido da API é convertido no modelo de entrada da camada de
/// serviços e cada modelo devolvido pelos serviços é convertido no DTO da resposta com todos os campos, com o mesmo nome
/// e o mesmo valor, para que o contrato da API (documentação da API v02) não mude.
/// </summary>
public sealed class ApiMappingsTests
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 18, 10, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Gets the conversions of the API layer that copy every field by name: the <c>ToDto</c> and <c>ToInput</c>
    /// extension methods of the classes in <see cref="Katch.Backend.Controllers.Mapping"/>.
    /// </summary>
    public static TheoryData<string> FieldByFieldConversions
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (MethodInfo method in Conversions())
            {
                data.Add(Describe(method));
            }

            return data;
        }
    }

    /// <summary>
    /// Todas as conversões estão cobertas por este teste: não há métodos públicos nas classes de conversão além dos
    /// <c>ToDto</c>, <c>ToInput</c>, da conversão das preferências e da página da Empresa.
    /// </summary>
    [Fact]
    public void EveryPublicConversionIsKnown()
    {
        string[] others = typeof(CompanyPageMapper).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(CompanyPageMapper).Namespace && type.IsPublic)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.Name is not ("ToDto" or "ToInput"))
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["ToPageDto", "ToPreferences"], others);
        Assert.NotEmpty(Conversions());
    }

    /// <summary>
    /// Cada conversão copia todos os campos, pelo nome, incluindo os elementos das listas e os objetos encaixados, e o
    /// resultado não tem campos a mais nem a menos do que a origem.
    /// </summary>
    /// <param name="conversion">Conversão, na forma <c>Origem -&gt; Destino</c>.</param>
    [Theory]
    [MemberData(nameof(FieldByFieldConversions))]
    public void ConversionCopiesEveryField(string conversion)
    {
        MethodInfo method = Conversions().Single(candidate => Describe(candidate) == conversion);
        object source = Sample(method.GetParameters()[0].ParameterType, new SampleSeed());

        object? result = method.Invoke(null, [source]);

        Assert.NotNull(result);
        Assert.IsType(method.ReturnType, result);
        Assert.Equivalent(source, result, strict: true);
    }

    /// <summary>
    /// Cada conversão recusa a origem nula, em vez de devolver uma resposta vazia.
    /// </summary>
    /// <param name="conversion">Conversão, na forma <c>Origem -&gt; Destino</c>.</param>
    [Theory]
    [MemberData(nameof(FieldByFieldConversions))]
    public void ConversionRejectsANullSource(string conversion)
    {
        MethodInfo method = Conversions().Single(candidate => Describe(candidate) == conversion);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [null]));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    /// <summary>
    /// A sessão é devolvida com o token, a validade, o tipo de conta e o identificador da conta (6.1.3).
    /// </summary>
    [Fact]
    public void SessionKeepsTheTokenAndTheAccount()
    {
        Guid userId = Guid.NewGuid();

        LoginResponse response = new LoginResult("token.fictício", Moment, UserType.Recruiter, userId).ToDto();

        Assert.Equal(new LoginResponse("token.fictício", Moment, UserType.Recruiter, userId), response);
    }

    /// <summary>
    /// As preferências sem modos de trabalho nem tipos de contrato chegam ao serviço com listas vazias (6.2.3); com
    /// valores, chegam com os mesmos valores.
    /// </summary>
    [Fact]
    public void PreferencesWithoutListsBecomeEmptyLists()
    {
        SearchPreferencesModel empty = UpdatePreferencesRequest.Empty.ToPreferences();
        SearchPreferencesModel filled = new UpdatePreferencesRequest(30, 950.5m, [WorkMode.Remote], [ContractType.Internship]).ToPreferences();

        Assert.Null(empty.MaxDistanceKm);
        Assert.Null(empty.MinSalaryExpectation);
        Assert.Empty(empty.WorkModes);
        Assert.Empty(empty.ContractTypes);
        Assert.Equal(30, filled.MaxDistanceKm);
        Assert.Equal(950.5m, filled.MinSalaryExpectation);
        Assert.Equal([WorkMode.Remote], filled.WorkModes);
        Assert.Equal([ContractType.Internship], filled.ContractTypes);
        Assert.Throws<ArgumentNullException>(() => ((UpdatePreferencesRequest)null!).ToPreferences());
    }

    /// <summary>
    /// O erro de um campo é devolvido com o mesmo campo e o mesmo motivo (secção 4 da documentação da API).
    /// </summary>
    [Fact]
    public void ValidationFieldsKeepTheFieldAndTheReason()
    {
        IReadOnlyList<FieldError> fields = [new("email", "obrigatório"), new("phoneNumber", "inválido")];

        ApiErrorDto body = Katch.Backend.Infrastructure.Errors.ApiErrorResponse.ValidationFailed(fields);

        Assert.Equal([new FieldErrorDto("email", "obrigatório"), new FieldErrorDto("phoneNumber", "inválido")], body.Fields!);
    }

    private static IEnumerable<MethodInfo> Conversions()
    {
        return typeof(CompanyPageMapper).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(CompanyPageMapper).Namespace && type.IsPublic)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.Name is "ToDto" or "ToInput" && method.GetParameters().Length == 1)
            .OrderBy(Describe, StringComparer.Ordinal);
    }

    private static string Describe(MethodInfo method)
    {
        return $"{method.GetParameters()[0].ParameterType.Name} -> {method.ReturnType.Name}";
    }

    private static object Sample(Type type, SampleSeed seed)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return Sample(underlying, seed);
        }

        int next = seed.Next();
        if (type == typeof(string))
        {
            return $"valor-{next}";
        }

        if (type == typeof(Guid))
        {
            return new Guid(next, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]);
        }

        if (type == typeof(int))
        {
            return next;
        }

        if (type == typeof(decimal))
        {
            return next + 0.5m;
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(DateTimeOffset))
        {
            return Moment.AddMinutes(next);
        }

        if (type.IsEnum)
        {
            Array values = Enum.GetValues(type);
            return values.GetValue(next % values.Length)!;
        }

        if (type.IsGenericType && type.GetGenericArguments().Length == 1
            && type.GetGenericTypeDefinition() is var definition
            && (definition == typeof(List<>) || definition == typeof(IReadOnlyList<>)))
        {
            Type item = type.GetGenericArguments()[0];
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(item))!;
            list.Add(Sample(item, seed));
            list.Add(Sample(item, seed));
            return list;
        }

        ConstructorInfo constructor = type.GetConstructors().OrderByDescending(candidate => candidate.GetParameters().Length).First();
        return constructor.Invoke(constructor.GetParameters().Select(parameter => Sample(parameter.ParameterType, seed)).ToArray());
    }

    private sealed class SampleSeed
    {
        private int value;

        public int Next()
        {
            return ++this.value;
        }
    }
}
