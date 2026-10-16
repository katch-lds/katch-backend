// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Repositories.Configuration;

namespace Katch.Backend.UnitTests.Repositories.Configuration;

/// <summary>
/// Testes da conversão dos nomes das enumerações para os valores do modelo de dados (secção 5.1).
/// </summary>
public sealed class UpperSnakeCaseNameTranslatorTests
{
    private readonly UpperSnakeCaseNameTranslator translator = new();

    /// <summary>
    /// Os membros das enumerações passam a maiúsculas separadas por <c>_</c>.
    /// </summary>
    /// <param name="clrName">Nome do membro em C#.</param>
    /// <param name="expected">Valor do tipo enumerado no PostgreSQL.</param>
    [Theory]
    [InlineData("Candidate", "CANDIDATE")]
    [InlineData("OnSite", "ON_SITE")]
    [InlineData("FifteenDays", "FIFTEEN_DAYS")]
    [InlineData("ServiceProvision", "SERVICE_PROVISION")]
    [InlineData("DeclinedByCandidate", "DECLINED_BY_CANDIDATE")]
    [InlineData("AccountBlockedOrSuspended", "ACCOUNT_BLOCKED_OR_SUSPENDED")]
    public void TranslateMemberNameReturnsUpperSnakeCase(string clrName, string expected)
    {
        Assert.Equal(expected, this.translator.TranslateMemberName(clrName));
    }

    /// <summary>
    /// Os nomes dos tipos ficam em <c>snake_case</c> minúsculo.
    /// </summary>
    [Fact]
    public void TranslateTypeNameReturnsLowerSnakeCase()
    {
        Assert.Equal("conversation_close_reason", this.translator.TranslateTypeName("ConversationCloseReason"));
    }
}
