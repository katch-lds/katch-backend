// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Reflection;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Duplo de teste genérico de uma interface de serviço, para os testes dos controllers: regista cada chamada (nome do
/// método e argumentos) e devolve o resultado configurado para o método, ou o valor por omissão do tipo. Evita escrever
/// um duplo à mão por interface, com dezenas de métodos que o controller em teste não usa.
/// </summary>
/// <typeparam name="TService">Interface do serviço.</typeparam>
public sealed class ServiceDouble<TService>
    where TService : class
{
    private readonly Dictionary<string, object?> results = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> failures = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceDouble{TService}"/> class.
    /// </summary>
    public ServiceDouble()
    {
        this.Service = DispatchProxy.Create<TService, ServiceDoubleProxy>();
        ((ServiceDoubleProxy)(object)this.Service).Handler = this.Handle;
    }

    /// <summary>
    /// Gets a implementação a passar ao controller.
    /// </summary>
    public TService Service { get; }

    /// <summary>
    /// Gets as chamadas recebidas, pela ordem.
    /// </summary>
    public List<ServiceCall> Calls { get; } = [];

    /// <summary>
    /// Define o resultado devolvido por um método (embrulhado numa <see cref="Task{TResult}"/> se o método for
    /// assíncrono).
    /// </summary>
    /// <param name="method">Nome do método.</param>
    /// <param name="result">Resultado.</param>
    /// <returns>O próprio duplo.</returns>
    public ServiceDouble<TService> Returns(string method, object? result)
    {
        this.results[method] = result;
        return this;
    }

    /// <summary>
    /// Define a exceção com que um método termina (numa tarefa falhada, se o método for assíncrono).
    /// </summary>
    /// <param name="method">Nome do método.</param>
    /// <param name="exception">Exceção.</param>
    /// <returns>O próprio duplo.</returns>
    public ServiceDouble<TService> Throws(string method, Exception exception)
    {
        this.failures[method] = exception;
        return this;
    }

    /// <summary>
    /// Devolve a única chamada recebida, que tem de ser ao método indicado.
    /// </summary>
    /// <param name="method">Nome do método esperado.</param>
    /// <returns>A chamada.</returns>
    public ServiceCall SingleCall(string method)
    {
        ServiceCall call = Assert.Single(this.Calls);
        Assert.Equal(method, call.Method);
        return call;
    }

    private static object? DefaultOf(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private object? Handle(MethodInfo method, object?[] arguments)
    {
        this.Calls.Add(new ServiceCall(method.Name, arguments));
        Type returnType = method.ReturnType;
        bool isTask = typeof(Task).IsAssignableFrom(returnType);
        Type? resultType = returnType.IsGenericType ? returnType.GetGenericArguments()[0] : null;

        if (this.failures.TryGetValue(method.Name, out Exception? failure))
        {
            if (!isTask)
            {
                throw failure;
            }

            return resultType is null
                ? Task.FromException(failure)
                : typeof(Task).GetMethod(nameof(Task.FromException), 1, [typeof(Exception)])!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, [failure]);
        }

        if (!isTask)
        {
            return this.results.TryGetValue(method.Name, out object? plain) ? plain : DefaultOf(returnType);
        }

        if (resultType is null)
        {
            return Task.CompletedTask;
        }

        object? result = this.results.TryGetValue(method.Name, out object? configured) ? configured : DefaultOf(resultType);
        return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [result]);
    }
}
