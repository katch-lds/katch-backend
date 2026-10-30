// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Services;

/// <summary>
/// A Empresa do Recrutador deixou de estar aprovada entre a verificação da política <c>ApprovedCompany</c> e a operação
/// (por exemplo, foi suspensa nesse intervalo). É convertida em <c>403</c> ERR-07, como a própria política, em vez de
/// subir como erro interno.
/// </summary>
public sealed class CompanyNotApprovedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyNotApprovedException"/> class.
    /// </summary>
    /// <param name="status">Estado atual da Empresa.</param>
    public CompanyNotApprovedException(CompanyStatus status)
        : base("A operação exige a Empresa aprovada.")
    {
        this.Status = status;
    }

    /// <summary>
    /// Gets the current status of the company.
    /// </summary>
    public CompanyStatus Status { get; }
}
