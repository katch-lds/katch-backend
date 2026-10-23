// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Domain;

/// <summary>
/// Objeto de valor com os oito dados de registo da Empresa (RF041), usado no registo, na nova submissão (RF045) e na
/// alteração dos dados da Empresa aprovada (RF049). As regras de formato são aplicadas pela <see cref="Company"/>.
/// </summary>
public sealed record CompanyRegistration
{
    /// <summary>
    /// Gets the registered name of the company (<c>company_name</c>), not empty and with at most 200 characters.
    /// </summary>
    public required string CompanyName { get; init; }

    /// <summary>
    /// Gets the tax identification number (<c>tax_id</c>), with exactly 9 digits (P04).
    /// </summary>
    public required string TaxId { get; init; }

    /// <summary>
    /// Gets the industry of the company (<c>industry</c>), from the pre-defined list (P15).
    /// </summary>
    public required Industry Industry { get; init; }

    /// <summary>
    /// Gets the address (<c>address</c>), not empty and with at most 255 characters.
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// Gets the identifier of the location of the company (<c>location_id</c>).
    /// </summary>
    public required Guid LocationId { get; init; }

    /// <summary>
    /// Gets the contact e-mail address (<c>contact_email</c>), in the format local@domain, with at most 255 characters.
    /// </summary>
    public required string ContactEmail { get; init; }

    /// <summary>
    /// Gets the contact phone number (<c>contact_phone</c>), with exactly 9 digits (P04).
    /// </summary>
    public required string ContactPhone { get; init; }

    /// <summary>
    /// Gets the name of the person responsible for the company (<c>responsible_name</c>), not empty and with at most
    /// 150 characters.
    /// </summary>
    public required string ResponsibleName { get; init; }
}
