// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure;

/// <summary>
/// Ponto de acesso indicado pelo cliente no cabeçalho <c>X-Client-App</c> do início de sessão: a aplicação móvel, só
/// para o Candidato, ou a área de gestão web, só para o Recrutador e o Administrador (documentação da API v02,
/// secção 3.2; modelo de classes do backend v02, secção 5.2). Não é persistida nem faz parte de nenhum DTO.
/// </summary>
public enum ClientApp
{
    /// <summary>
    /// Aplicação móvel (<c>X-Client-App: mobile</c>).
    /// </summary>
    Mobile,

    /// <summary>
    /// Área de gestão web (<c>X-Client-App: web</c>).
    /// </summary>
    Web,
}
