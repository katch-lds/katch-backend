// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Services;

/// <summary>
/// Ficheiro a devolver ao cliente (modelo de classes do backend v02, secção 5.1).
/// </summary>
/// <param name="Content">Conteúdo do ficheiro; é libertado pela resposta HTTP.</param>
/// <param name="ContentType">Tipo de conteúdo real: <c>image/jpeg</c>, <c>image/png</c> ou <c>application/pdf</c>.</param>
/// <param name="FileName">Nome a apresentar.</param>
public sealed record FileContent(Stream Content, string ContentType, string FileName);
