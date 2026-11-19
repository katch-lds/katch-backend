// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Services.Models;

/// <summary>
/// Conta de utilizador na lista do Administrador (documentação da API v02, secções 6.10.1 e 8.5; RF085).
/// Modelo da camada de serviços; a conversão de e para o formato da API é feita na camada da API (I138).
/// </summary>
/// <param name="UserId">Identificador da conta.</param>
/// <param name="Email">Correio eletrónico da conta.</param>
/// <param name="UserType">Tipo de conta: Candidato, Recrutador ou Administrador.</param>
/// <param name="Status">Estado da conta: ativa, bloqueada ou suspensa.</param>
public sealed record AccountModel(Guid UserId, string Email, UserType UserType, AccountStatus Status);
