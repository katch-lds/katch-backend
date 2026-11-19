// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Dados da página de apresentação gravados pelo Recrutador (documentação da API v02, secções 6.4.5 e 8.3; RF046). Um
/// valor <see langword="null"/>, vazio ou só com espaços limpa o campo.
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Description">Descrição da atividade, com no máximo 1000 caracteres (P08).</param>
/// <param name="Website">Sítio na Internet, iniciado por <c>http://</c> ou <c>https://</c> e com no máximo 500 caracteres
/// (P10).</param>
public sealed record CompanyPageInput(string? Description, string? Website)
{
    /// <summary>
    /// Gets a request without any field, used when the request body is empty: limpa os dois campos.
    /// </summary>
    public static CompanyPageInput Empty { get; } = new(null, null);
}
