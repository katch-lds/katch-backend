// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Services.Models;

/// <summary>
/// Resposta de uma autenticação bem-sucedida (documentação da API v02, secção 3.1).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="Token">Credencial JWT, a enviar em <c>Authorization: Bearer</c>.</param>
/// <param name="ExpiresAt">Instante em que a credencial expira, em UTC.</param>
/// <param name="UserType">Tipo de conta autenticada.</param>
/// <param name="UserId">Identificador da conta autenticada.</param>
public sealed record LoginResult(string Token, DateTimeOffset ExpiresAt, UserType UserType, Guid UserId);
