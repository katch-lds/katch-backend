// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services.Models;

/// <summary>
/// Erro do catálogo da API (documentação da API v02, secção 4.3) indicado pela camada de serviços quando a operação não
/// é possível no estado atual do elemento. A camada da API converte-o no corpo da resposta <c>409</c> (I138).
/// </summary>
/// <param name="Code">Código do erro do catálogo, por exemplo <c>ERR-30</c>.</param>
/// <param name="Message">Mensagem a apresentar ao utilizador.</param>
public sealed record ServiceError(string Code, string Message);
