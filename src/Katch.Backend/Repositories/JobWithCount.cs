// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Uma vaga publicada com o número de interesses, para a lista global do Administrador (modelo de classes do backend v02,
/// secção 4: entidade e contagem; RF098). A designação da Empresa é obtida pelo serviço.
/// </summary>
/// <param name="Job">Vaga, lida sem acompanhamento do contexto.</param>
/// <param name="InterestCount">Número de interesses na vaga (<c>match</c> diferente de <c>DECLINED_BY_CANDIDATE</c>).</param>
public sealed record JobWithCount(Job Job, int InterestCount);
