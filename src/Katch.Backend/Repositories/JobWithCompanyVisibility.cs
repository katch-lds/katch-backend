// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.Repositories;

/// <summary>
/// Uma vaga e a indicação de a sua Empresa estar visível aos Candidatos, para verificar a visibilidade da vaga (RF014).
/// </summary>
/// <param name="Job">Vaga, lida sem acompanhamento do contexto.</param>
/// <param name="CompanyVisible">Se a Empresa está aprovada e o Recrutador tem a conta ativa.</param>
public sealed record JobWithCompanyVisibility(Job Job, bool CompanyVisible);
