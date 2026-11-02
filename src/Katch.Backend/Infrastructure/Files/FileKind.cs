// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Files;

/// <summary>
/// Tipo de ficheiro guardado pela aplicação (documentação da API v02, secção 2.6). Na rota e nos URLs aparece em texto
/// com maiúsculas e <c>_</c> (<c>PROFILE_PHOTO</c>, <c>COMPANY_LOGO</c>).
/// </summary>
public enum FileKind
{
    /// <summary>
    /// Fotografia de perfil do Candidato: JPEG ou PNG, até 5 MB.
    /// </summary>
    ProfilePhoto,

    /// <summary>
    /// Curriculum vitae do Candidato: PDF, até 5 MB. Nunca é obtido por <c>/api/files</c> (RNF007).
    /// </summary>
    CurriculumVitae,

    /// <summary>
    /// Logótipo da Empresa: JPEG ou PNG, até 2 MB.
    /// </summary>
    CompanyLogo,

    /// <summary>
    /// Fotografia da galeria da Empresa: JPEG ou PNG, até 5 MB.
    /// </summary>
    CompanyPhoto,

    /// <summary>
    /// Fotografia de uma vaga: JPEG ou PNG, até 5 MB.
    /// </summary>
    JobPhoto,
}
