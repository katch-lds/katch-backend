// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Domain;

/// <summary>
/// Objeto de valor com os dados da vaga (RF050), usado na criação e na alteração (RF054). As regras são aplicadas pela
/// <see cref="Job"/>; as competências e os benefícios associados ficam nas tabelas <c>job_skill</c> e
/// <c>job_benefit</c> (modelo de classes do backend v02, secção 3.3).
/// </summary>
public sealed record JobData
{
    /// <summary>
    /// Gets the role (<c>title</c>), not empty and with at most 150 characters.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the description of the opportunity (<c>description</c>), not empty.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the minimum value of the salary range in euros (<c>min_salary</c>), positive (RF051).
    /// </summary>
    public required decimal MinSalary { get; init; }

    /// <summary>
    /// Gets the maximum value of the salary range in euros (<c>max_salary</c>), greater than or equal to the minimum
    /// (RF051).
    /// </summary>
    public required decimal MaxSalary { get; init; }

    /// <summary>
    /// Gets the identifier of the location of the job (<c>location_id</c>), from the pre-defined list.
    /// </summary>
    public required Guid LocationId { get; init; }

    /// <summary>
    /// Gets the type of contract (<c>contract_type</c>, P09).
    /// </summary>
    public required ContractType ContractType { get; init; }

    /// <summary>
    /// Gets the work mode (<c>work_mode</c>, P16).
    /// </summary>
    public required WorkMode WorkMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the job is urgent (<c>is_urgent</c>); <see langword="false"/> by default.
    /// </summary>
    public bool IsUrgent { get; init; }

    /// <summary>
    /// Gets the optional publication deadline (<c>expires_at</c>, RF057).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
