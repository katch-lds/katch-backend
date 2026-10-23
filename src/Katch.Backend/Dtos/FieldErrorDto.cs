// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Dtos;

/// <summary>
/// Erro de validação de um campo (documentação da API v02, secção 4.1).
/// </summary>
/// <param name="Field">Nome do campo.</param>
/// <param name="Reason">Motivo da recusa.</param>
public sealed record FieldErrorDto(string Field, string Reason);
