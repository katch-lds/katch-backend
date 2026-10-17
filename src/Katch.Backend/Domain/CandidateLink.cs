// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain;

/// <summary>
/// Hiperligação profissional do Candidato (tabela <c>candidate_link</c>), até três por Candidato (RF012, P10).
/// O endereço é guardado tal como indicado, sem importar o conteúdo da página.
/// </summary>
public sealed class CandidateLink
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateLink"/> class. Só o <see cref="Candidate"/> cria hiperligações.
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato.</param>
    /// <param name="position">Posição, de 1 a 3.</param>
    /// <param name="url">Endereço iniciado por <c>http://</c> ou <c>https://</c>.</param>
    internal CandidateLink(Guid candidateId, short position, string url)
    {
        this.Id = Guid.NewGuid();
        this.CandidateId = candidateId;
        this.Position = position;
        this.Url = url;
    }

    private CandidateLink()
    {
        this.Url = string.Empty;
    }

    /// <summary>
    /// Gets the identifier of the link (<c>id</c>).
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the identifier of the candidate that owns the link (<c>candidate_id</c>).
    /// </summary>
    public Guid CandidateId { get; private set; }

    /// <summary>
    /// Gets the position of the link, from 1 to 3 (<c>position</c>), unique per candidate; it limits the number
    /// of links and sets the order in which they are shown.
    /// </summary>
    public short Position { get; private set; }

    /// <summary>
    /// Gets the address of the link (<c>url</c>), starting with <c>http://</c> or <c>https://</c>.
    /// </summary>
    public string Url { get; private set; }
}
