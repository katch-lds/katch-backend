// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Uma vaga publicada com a designação da sua Empresa e o número de interesses, para a lista global do Administrador
/// (modelo de classes do backend v02, secção 4; RF098). A <see cref="Job"/> só guarda o identificador da Empresa, por
/// isso a designação vem da mesma consulta.
/// </summary>
/// <param name="Job">Vaga, lida sem acompanhamento do contexto.</param>
/// <param name="CompanyName">Designação social da Empresa da vaga.</param>
/// <param name="InterestCount">Número de interesses na vaga (<c>match</c> diferente de <c>DECLINED_BY_CANDIDATE</c>).</param>
public sealed record JobWithCount(Job Job, string CompanyName, int InterestCount);
