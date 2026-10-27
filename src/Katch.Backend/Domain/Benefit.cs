// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain;

/// <summary>
/// Benefício da lista pré-definida mantida pelo Administrador (tabela <c>benefit</c>; modelo de dados v02, secção 6.9;
/// modelo de classes do backend v02, secção 3.2). É associado às vagas (<c>job_benefit</c>) sem classe de associação.
/// </summary>
/// <remarks>
/// A designação única sem distinção de maiúsculas (RF101, RF102) depende de outras linhas: é garantida pelo índice
/// <c>uq_benefit_name</c> sobre <c>lower(name)</c> e verificada pelo serviço. A autoria e a data da criação e da
/// alteração ficam no <c>OperationLog</c> (<c>BENEFIT_CREATED</c>, <c>BENEFIT_RENAMED</c>; DM-22).
/// </remarks>
public sealed class Benefit
{
    /// <summary>
    /// Comprimento máximo da designação (DM-12).
    /// </summary>
    public const int MaxNameLength = ReferenceListName.MaxLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Benefit"/> class (RF101).
    /// </summary>
    /// <param name="name">Designação, não vazia e com no máximo 100 caracteres; é guardada sem os espaços nas extremidades.</param>
    /// <exception cref="ArgumentException">Se a designação estiver vazia ou for demasiado longa.</exception>
    public Benefit(string name)
    {
        this.Name = ReferenceListName.Normalize(name, nameof(name));
        this.Id = Guid.NewGuid();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Benefit"/> class. Usado pelo Entity Framework Core.
    /// </summary>
    private Benefit()
    {
        this.Name = string.Empty;
    }

    /// <summary>
    /// Gets the identifier of the benefit (<c>id</c>).
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the name of the benefit (<c>name</c>), unique without case distinction.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Altera a designação do benefício (RF102); a alteração aparece em todas as vagas que o usam.
    /// </summary>
    /// <param name="name">Nova designação, não vazia e com no máximo 100 caracteres.</param>
    /// <exception cref="ArgumentException">Se a designação estiver vazia ou for demasiado longa.</exception>
    public void Rename(string name)
    {
        this.Name = ReferenceListName.Normalize(name, nameof(name));
    }
}
