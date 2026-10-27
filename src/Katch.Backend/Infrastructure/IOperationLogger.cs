// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Registo de autoria das operações do RF112 no <c>operation_log</c> (modelo de classes do backend v02, secção 5.2).
/// A linha é acrescentada ao contexto do pedido e gravada pelo serviço na mesma gravação da operação, para que a
/// operação e o registo fiquem os dois gravados ou nenhum.
/// </summary>
public interface IOperationLogger
{
    /// <summary>
    /// Acrescenta uma linha ao <c>operation_log</c>, com o instante atual.
    /// </summary>
    /// <param name="userId">Autor da operação; <see langword="null"/> só no encerramento automático de vaga (RF057).</param>
    /// <param name="operation">Operação do RF112.</param>
    /// <param name="entityId">Identificador do elemento a que a operação se refere (conta, Empresa, vaga, ...).</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando a linha foi acrescentada.</returns>
    public Task LogAsync(
        Guid? userId,
        OperationType operation,
        Guid entityId,
        CancellationToken cancellationToken = default);
}
