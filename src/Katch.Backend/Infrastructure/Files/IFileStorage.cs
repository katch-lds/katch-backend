// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Armazenamento dos ficheiros enviados (modelo de classes do backend v02, secção 5.2; RNF007). Os caminhos são sempre
/// relativos, no formato <c>&lt;tipo&gt;/&lt;nome&gt;</c>, e nunca saem da raiz configurada.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Guarda o conteúdo na pasta do tipo, com um nome gerado pelo servidor (identificador aleatório e a extensão do
    /// formato real do conteúdo).
    /// </summary>
    /// <param name="content">Conteúdo do ficheiro, já validado.</param>
    /// <param name="kind">Tipo de ficheiro.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O caminho relativo <c>&lt;tipo&gt;/&lt;nome&gt;</c>.</returns>
    /// <exception cref="ArgumentException">Se o conteúdo não for de um formato aceite.</exception>
    public Task<string> SaveAsync(Stream content, FileKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre o ficheiro para leitura.
    /// </summary>
    /// <param name="path">Caminho relativo <c>&lt;tipo&gt;/&lt;nome&gt;</c>.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O conteúdo, que quem chama liberta.</returns>
    /// <exception cref="FileNotFoundException">Se o caminho for inválido, sair da raiz ou o ficheiro não existir.</exception>
    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina o ficheiro; um ficheiro que já não existe não é erro.
    /// </summary>
    /// <param name="path">Caminho relativo <c>&lt;tipo&gt;/&lt;nome&gt;</c>.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Tarefa que termina quando o ficheiro foi eliminado.</returns>
    /// <exception cref="ArgumentException">Se o caminho for inválido ou sair da raiz.</exception>
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
