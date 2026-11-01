// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Reflection;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Proxy criado pelo <see cref="DispatchProxy"/> para o <see cref="ServiceDouble{TService}"/>: encaminha cada chamada
/// para o duplo. Tem de ser público, não selado e com construtor sem parâmetros.
/// </summary>
public class ServiceDoubleProxy : DispatchProxy
{
    /// <summary>
    /// Gets or sets o tratamento das chamadas, definido pelo duplo.
    /// </summary>
    internal Func<MethodInfo, object?[], object?> Handler { get; set; } = (_, _) => null;

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        return this.Handler(targetMethod, args ?? []);
    }
}
