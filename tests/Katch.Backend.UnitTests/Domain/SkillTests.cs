// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="Skill"/>: criação e alteração da designação (RF099, RF100).
/// </summary>
public sealed class SkillTests
{
    /// <summary>
    /// Uma competência nova tem identificador e a designação sem os espaços nas extremidades.
    /// </summary>
    [Fact]
    public void NewSkillHasIdentifierAndTrimmedName()
    {
        var skill = new Skill("  C#  ");

        Assert.NotEqual(Guid.Empty, skill.Id);
        Assert.Equal("C#", skill.Name);
    }

    /// <summary>
    /// A designação com 100 caracteres é aceite.
    /// </summary>
    [Fact]
    public void NameWithOneHundredCharactersIsAccepted()
    {
        var skill = new Skill(new string('a', Skill.MaxNameLength));

        Assert.Equal(Skill.MaxNameLength, skill.Name.Length);
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
        Assert.Throws<ArgumentException>(() => new Skill(name!));
    }

    /// <summary>
    /// Uma designação com mais de 100 caracteres é rejeitada.
    /// </summary>
    [Fact]
    public void NameLongerThanTheColumnIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Skill(new string('a', Skill.MaxNameLength + 1)));
    }

    /// <summary>
    /// Alterar a designação grava a nova, sem os espaços nas extremidades, e mantém o identificador.
    /// </summary>
    [Fact]
    public void RenameChangesTheNameAndKeepsTheIdentifier()
    {
        var skill = new Skill("Java Script");
        Guid id = skill.Id;

        skill.Rename(" JavaScript ");

        Assert.Equal(id, skill.Id);
        Assert.Equal("JavaScript", skill.Name);
    }

    /// <summary>
    /// Uma alteração inválida é rejeitada e a designação anterior mantém-se.
    /// </summary>
    [Fact]
    public void InvalidRenameKeepsThePreviousName()
    {
        var skill = new Skill("SQL");

        Assert.Throws<ArgumentException>(() => skill.Rename(" "));
        Assert.Equal("SQL", skill.Name);
    }
}
