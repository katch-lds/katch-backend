// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure;

/// <summary>
/// I066, prova de bloqueio QG-05: palavra-passe escrita no código, que a regra S2068 do SonarQube assinala como
/// problema de segurança e faz falhar o Quality Gate (não integrar). O valor é fictício.
/// </summary>
internal static class ProvaCredencialNoCodigo
{
    /// <summary>
    /// Cadeia de ligação com a palavra-passe no código.
    /// </summary>
    internal const string ConnectionString = "Host=localhost;Database=katch;Username=postgres;Password=valor-ficticio-i066";
}
