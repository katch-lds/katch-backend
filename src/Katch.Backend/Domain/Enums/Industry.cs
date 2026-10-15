// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Setor de atividade da Empresa (parâmetro P15). Tipo PostgreSQL <c>industry</c>.
/// </summary>
public enum Industry
{
    /// <summary>
    /// Tecnologia.
    /// </summary>
    Technology,

    /// <summary>
    /// Saúde.
    /// </summary>
    Healthcare,

    /// <summary>
    /// Comércio.
    /// </summary>
    Retail,

    /// <summary>
    /// Hotelaria e restauração.
    /// </summary>
    Hospitality,

    /// <summary>
    /// Construção.
    /// </summary>
    Construction,

    /// <summary>
    /// Indústria.
    /// </summary>
    Manufacturing,

    /// <summary>
    /// Educação.
    /// </summary>
    Education,

    /// <summary>
    /// Finanças.
    /// </summary>
    Finance,

    /// <summary>
    /// Logística.
    /// </summary>
    Logistics,

    /// <summary>
    /// Serviços.
    /// </summary>
    Services,

    /// <summary>
    /// Outro.
    /// </summary>
    Other,
}
