// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="Benefit"/>: criação e alteração da designação (RF101, RF102).
/// </summary>
public sealed class BenefitTests
{
    /// <summary>
    /// Um benefício novo tem identificador e a designação sem os espaços nas extremidades.
    /// </summary>
    [Fact]
    public void NewBenefitHasIdentifierAndTrimmedName()
    {
        var benefit = new Benefit(" Seguro de saúde ");

        Assert.NotEqual(Guid.Empty, benefit.Id);
        Assert.Equal("Seguro de saúde", benefit.Name);
    }

    /// <summary>
    /// Uma designação vazia ou só com espaços é rejeitada.
    /// </summary>
    /// <param name="name">Designação inválida.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyNameIsRejected(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Benefit(name!));
    }

    /// <summary>
    /// Uma designação com mais de 100 caracteres é rejeitada.
    /// </summary>
    [Fact]
    public void NameLongerThanTheColumnIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Benefit(new string('b', Benefit.MaxNameLength + 1)));
    }

    /// <summary>
    /// Alterar a designação grava a nova e mantém o identificador; uma alteração inválida mantém a anterior.
    /// </summary>
    [Fact]
    public void RenameChangesTheNameAndInvalidRenameKeepsIt()
    {
        var benefit = new Benefit("Teletrabalho");
        Guid id = benefit.Id;

        benefit.Rename("Trabalho remoto");

        Assert.Equal(id, benefit.Id);
        Assert.Equal("Trabalho remoto", benefit.Name);
        Assert.Throws<ArgumentException>(() => benefit.Rename(string.Empty));
        Assert.Equal("Trabalho remoto", benefit.Name);
    }
}
