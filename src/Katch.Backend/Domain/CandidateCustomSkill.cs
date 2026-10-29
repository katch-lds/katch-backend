// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.RegularExpressions;

namespace Katch.Backend.Domain;

/// <summary>
/// Competência indicada pelo Candidato na opção «Outro», fora da lista pré-definida (tabela
/// <c>candidate_custom_skill</c>; modelo de dados v02, secção 6.8, e DM-16; modelo de classes do backend v02, secção
/// 3.2). É criada e removida só através de <see cref="Candidate.AddCustomSkill(string)"/> e
/// <see cref="Candidate.RemoveCustomSkill(Guid)"/>.
/// </summary>
/// <remarks>
/// A designação tem 2 a 40 caracteres, apenas letras, algarismos, espaços e os símbolos + # . - (P07, RF009). A
/// unicidade por Candidato, sem distinção de maiúsculas, é verificada pelo <see cref="Candidate"/> e garantida pelo
/// índice <c>uq_candidate_custom_skill</c> sobre (<c>candidate_id</c>, <c>lower(label)</c>).
/// </remarks>
public sealed partial class CandidateCustomSkill
{
    /// <summary>
    /// Comprimento mínimo da designação (P07).
    /// </summary>
    public const int MinLabelLength = 2;

    /// <summary>
    /// Comprimento máximo da designação (P07).
    /// </summary>
    public const int MaxLabelLength = 40;

    /// <summary>
    /// Mensagem de rejeição de uma designação fora do P07, igual à do catálogo da API v02 (secção 4.3).
    /// </summary>
    internal const string InvalidLabelMessage =
        "A competência tem de ter entre 2 e 40 caracteres e conter apenas letras, algarismos, espaços e os símbolos + # . -";

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateCustomSkill"/> class.
    /// </summary>
    /// <param name="candidateId">Identificador do Candidato que indicou a competência.</param>
    /// <param name="label">Designação já validada por <see cref="Normalize(string)"/>.</param>
    internal CandidateCustomSkill(Guid candidateId, string label)
    {
        this.Id = Guid.NewGuid();
        this.CandidateId = candidateId;
        this.Label = label;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateCustomSkill"/> class. Usado pelo Entity Framework Core.
    /// </summary>
    private CandidateCustomSkill()
    {
        this.Label = string.Empty;
    }

    /// <summary>
    /// Gets the identifier of the custom skill (<c>id</c>).
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the identifier of the candidate (<c>candidate_id</c>).
    /// </summary>
    public Guid CandidateId { get; private set; }

    /// <summary>
    /// Gets the label (<c>label</c>), from 2 to 40 characters, unique per candidate without case distinction.
    /// </summary>
    public string Label { get; private set; }

    /// <summary>
    /// Valida a designação segundo o P07 e devolve-a sem os espaços nas extremidades.
    /// </summary>
    /// <param name="label">Designação indicada pelo Candidato.</param>
    /// <returns>A designação sem os espaços nas extremidades.</returns>
    /// <exception cref="ArgumentException">Se a designação não cumprir o P07.</exception>
    internal static string Normalize(string label)
    {
        string trimmed = label?.Trim() ?? string.Empty;

        if (trimmed.Length < MinLabelLength || trimmed.Length > MaxLabelLength || !LabelPattern().IsMatch(trimmed))
        {
            throw new ArgumentException(InvalidLabelMessage, nameof(label));
        }

        return trimmed;
    }

    // Letras (incluindo as acentuadas), algarismos de 0 a 9, espaços e + # . - (P07). Os algarismos de outras escritas
    // (por exemplo, «٣») ficam de fora: a classe [[:alnum:]] do CHECK só os aceita em algumas locales da base.
    [GeneratedRegex(@"^[\p{L}0-9 +#.\-]+\z")]
    private static partial Regex LabelPattern();
}
