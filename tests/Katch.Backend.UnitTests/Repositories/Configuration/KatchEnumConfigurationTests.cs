// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories.Configuration;

namespace Katch.Backend.UnitTests.Repositories.Configuration;

/// <summary>
/// Testes que comparam as enumerações do domínio com os tipos enumerados da secção 5.1 do modelo de dados v02
/// (<c>m3-modelo-de-dados-katch-v02.md</c>). Os testes de integração correm sobre SQLite, que ignora os tipos
/// enumerados, pelo que estes testes são a verificação automática dos valores e da sua ordem.
/// </summary>
public sealed class KatchEnumConfigurationTests
{
    /// <summary>
    /// Gets the enumerations of the domain with the values of section 5.1 of the data model, in the same order.
    /// </summary>
    public static TheoryData<Type, string[]> DataModelEnums { get; } = new()
    {
        { typeof(UserType), ["CANDIDATE", "RECRUITER", "ADMIN"] },
        { typeof(AccountStatus), ["ACTIVE", "BLOCKED", "SUSPENDED"] },
        { typeof(WorkMode), ["ON_SITE", "HYBRID", "REMOTE"] },
        { typeof(ContractType), ["PERMANENT", "FIXED_TERM", "INTERNSHIP", "SERVICE_PROVISION"] },
        { typeof(Availability), ["IMMEDIATE", "FIFTEEN_DAYS", "ONE_MONTH", "TO_BE_AGREED"] },
        {
            typeof(Industry),
            [
                "TECHNOLOGY", "HEALTHCARE", "RETAIL", "HOSPITALITY", "CONSTRUCTION", "MANUFACTURING", "EDUCATION",
                "FINANCE", "LOGISTICS", "SERVICES", "OTHER",
            ]
        },
        { typeof(CompanyStatus), ["PENDING", "APPROVED", "REJECTED", "SUSPENDED"] },
        { typeof(JobStatus), ["UNPUBLISHED", "PUBLISHED", "SUSPENDED", "CLOSED"] },
        { typeof(MatchStatus), ["DECLINED_BY_CANDIDATE", "WAITING", "MATCHED", "REJECTED_BY_RECRUITER"] },
        { typeof(ConversationStatus), ["OPEN", "CLOSED"] },
        { typeof(ConversationCloseReason), ["CLOSED_BY_PARTY", "ACCOUNT_BLOCKED_OR_SUSPENDED", "COMPANY_SUSPENDED"] },
        {
            typeof(OperationType),
            [
                "COMPANY_SUBMITTED", "COMPANY_UPDATED", "COMPANY_APPROVED", "COMPANY_REJECTED", "COMPANY_SUSPENDED",
                "COMPANY_REACTIVATED", "JOB_CREATED", "JOB_PUBLISHED", "JOB_UPDATED", "JOB_SUSPENDED", "JOB_REPUBLISHED",
                "JOB_CLOSED", "JOB_DELETED", "ACCOUNT_BLOCKED", "ACCOUNT_REACTIVATED", "ACCOUNT_SUSPENDED",
                "SKILL_CREATED", "SKILL_RENAMED", "BENEFIT_CREATED", "BENEFIT_RENAMED",
            ]
        },
    };

    /// <summary>
    /// Cada enumeração produz, pela mesma ordem, os valores do tipo enumerado do modelo de dados.
    /// </summary>
    /// <param name="enumType">Enumeração do domínio.</param>
    /// <param name="expected">Valores do tipo enumerado na secção 5.1, pela ordem do DDL.</param>
    [Theory]
    [MemberData(nameof(DataModelEnums))]
    public void EnumMembersMatchDataModelValuesInOrder(Type enumType, string[] expected)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        string[] actual = [.. Enum.GetNames(enumType).Select(KatchEnumConfiguration.ValueTranslator.TranslateMemberName)];

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// O domínio tem exatamente as doze enumerações do modelo de dados v02, todas cobertas pelo teste anterior.
    /// </summary>
    [Fact]
    public void DomainHasExactlyTheDataModelEnums()
    {
        IEnumerable<Type> domainEnums = typeof(UserType).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.Namespace == typeof(UserType).Namespace);
        IEnumerable<Type> covered = DataModelEnums.Select(row => row.Data.Item1);

        Assert.Equal(12, covered.Count());
        Assert.Equal(
            covered.Select(type => type.Name).Order(StringComparer.Ordinal),
            domainEnums.Select(type => type.Name).Order(StringComparer.Ordinal));
    }
}
