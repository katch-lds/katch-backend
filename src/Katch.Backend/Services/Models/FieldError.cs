// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Erro de validação de um campo (documentação da API v02, secção 4.1).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Field">Nome do campo.</param>
/// <param name="Reason">Motivo da recusa.</param>
public sealed record FieldError(string Field, string Reason);
