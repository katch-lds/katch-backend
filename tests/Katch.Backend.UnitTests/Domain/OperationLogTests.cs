// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="OperationLog"/>: criação e regra do autor (modelo de dados v02, secções 6.15 e 7.1 e DM-19;
/// modelo de classes do backend v02, secção 3.4).
/// </summary>
public sealed class OperationLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly Guid EntityId = Guid.NewGuid();

    /// <summary>
    /// A linha nova guarda o autor, a operação, a entidade e a data; o identificador fica por atribuir, porque é gerado
    /// pela base de dados.
    /// </summary>
    [Fact]
    public void RecordCreatesALineWithoutIdentifier()
    {
        var log = OperationLog.Record(UserId, OperationType.CompanyApproved, EntityId, Now);

        Assert.Equal(0, log.Id);
        Assert.Equal(UserId, log.UserId);
        Assert.Equal(OperationType.CompanyApproved, log.Operation);
        Assert.Equal(EntityId, log.EntityId);
        Assert.Equal(Now, log.OccurredAt);
    }

    /// <summary>
    /// O encerramento automático da vaga pode ficar sem autor (<c>ck_operation_log_author</c>).
    /// </summary>
    [Fact]
    public void JobClosedWithoutAuthorIsAccepted()
    {
        var log = OperationLog.Record(null, OperationType.JobClosed, EntityId, Now);

        Assert.Null(log.UserId);
        Assert.Equal(OperationType.JobClosed, log.Operation);
    }

    /// <summary>
    /// O encerramento da vaga por uma conta também é aceite, com autor.
    /// </summary>
    [Fact]
    public void JobClosedWithAuthorIsAccepted()
    {
        Assert.Equal(UserId, OperationLog.Record(UserId, OperationType.JobClosed, EntityId, Now).UserId);
    }

    /// <summary>
    /// Qualquer operação diferente de <see cref="OperationType.JobClosed"/> exige autor.
    /// </summary>
    /// <param name="operation">Operação que exige autor.</param>
    [Theory]
    [InlineData(OperationType.CompanySubmitted)]
    [InlineData(OperationType.JobCreated)]
    [InlineData(OperationType.JobDeleted)]
    [InlineData(OperationType.AccountBlocked)]
    [InlineData(OperationType.BenefitRenamed)]
    public void OtherOperationsRequireAnAuthor(OperationType operation)
    {
        Assert.Throws<ArgumentException>(() => OperationLog.Record(null, operation, EntityId, Now));
    }

    /// <summary>
    /// Todas as operações da enumeração, exceto <see cref="OperationType.JobClosed"/>, rejeitam a falta de autor.
    /// </summary>
    [Fact]
    public void OnlyJobClosedAcceptsAMissingAuthor()
    {
        foreach (OperationType operation in Enum.GetValues<OperationType>())
        {
            if (operation == OperationType.JobClosed)
            {
                Assert.Null(OperationLog.Record(null, operation, EntityId, Now).UserId);
            }
            else
            {
                Assert.Throws<ArgumentException>(() => OperationLog.Record(null, operation, EntityId, Now));
            }
        }
    }

    /// <summary>
    /// O identificador da entidade é obrigatório, e o do autor, quando existe, não pode estar vazio.
    /// </summary>
    [Fact]
    public void EmptyIdentifiersAreRejected()
    {
        Assert.Throws<ArgumentException>(() => OperationLog.Record(UserId, OperationType.JobCreated, Guid.Empty, Now));
        Assert.Throws<ArgumentException>(() => OperationLog.Record(Guid.Empty, OperationType.JobCreated, EntityId, Now));
        Assert.Throws<ArgumentException>(() => OperationLog.Record(Guid.Empty, OperationType.JobClosed, EntityId, Now));
    }

    /// <summary>
    /// Uma operação fora da enumeração é rejeitada.
    /// </summary>
    [Fact]
    public void UndefinedOperationIsRejected()
    {
        Assert.Throws<ArgumentException>(() => OperationLog.Record(UserId, (OperationType)999, EntityId, Now));
    }

    /// <summary>
    /// A entidade só tem as propriedades do modelo de dados e nenhuma é alterável a partir de fora: o registo só admite
    /// inserções e não guarda o tipo da entidade (DM-19).
    /// </summary>
    [Fact]
    public void ExposesNoSettersAndNoEntityType()
    {
        System.Reflection.PropertyInfo[] properties = typeof(OperationLog).GetProperties();

        Assert.Equal(
            [nameof(OperationLog.EntityId), nameof(OperationLog.Id), nameof(OperationLog.OccurredAt), nameof(OperationLog.Operation), nameof(OperationLog.UserId)],
            properties.Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.All(properties, property => Assert.False(property.SetMethod?.IsPublic ?? false));
        System.Reflection.MethodInfo[] methods = typeof(OperationLog).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.All(methods, method => Assert.True(method.IsSpecialName));
    }
}
