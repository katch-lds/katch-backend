// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes das competências do <see cref="Candidate"/>: competências da lista pré-definida (RF008) e da opção «Outro»
/// (RF009, P07), segundo o modelo de classes do backend v02 (secção 3.2) e a DM-16.
/// </summary>
public sealed class CandidateSkillsTests
{
    private static readonly DateTimeOffset Registered = new(2026, 10, 23, 17, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Um Candidato novo não tem competências.
    /// </summary>
    [Fact]
    public void NewCandidateHasNoSkills()
    {
        Candidate candidate = CreateCandidate();

        Assert.Empty(candidate.Skills);
        Assert.Empty(candidate.CustomSkills);
    }

    /// <summary>
    /// As competências da lista são associadas ao perfil pela ordem em que são acrescentadas.
    /// </summary>
    [Fact]
    public void AddSkillAssociatesSkillsOfTheList()
    {
        Candidate candidate = CreateCandidate();
        var csharp = new Skill("C#");
        var sql = new Skill("SQL");

        candidate.AddSkill(csharp);
        candidate.AddSkill(sql);

        Assert.Equal([csharp, sql], candidate.Skills.ToArray());
    }

    /// <summary>
    /// A mesma competência da lista não pode ser associada duas vezes ao mesmo Candidato.
    /// </summary>
    [Fact]
    public void SameSkillCannotBeAddedTwice()
    {
        Candidate candidate = CreateCandidate();
        var csharp = new Skill("C#");
        candidate.AddSkill(csharp);

        Assert.Throws<ArgumentException>(() => candidate.AddSkill(csharp));
        Assert.Single(candidate.Skills);
    }

    /// <summary>
    /// Retirar uma competência associada remove-a e mantém as restantes.
    /// </summary>
    [Fact]
    public void RemoveSkillRemovesOnlyThatSkill()
    {
        Candidate candidate = CreateCandidate();
        var csharp = new Skill("C#");
        var sql = new Skill("SQL");
        candidate.AddSkill(csharp);
        candidate.AddSkill(sql);

        candidate.RemoveSkill(csharp.Id);

        Assert.Equal([sql], candidate.Skills.ToArray());
    }

    /// <summary>
    /// Retirar uma competência que não está associada ao perfil é rejeitado.
    /// </summary>
    [Fact]
    public void RemovingASkillThatIsNotAssociatedIsRejected()
    {
        Candidate candidate = CreateCandidate();

        Assert.Throws<KeyNotFoundException>(() => candidate.RemoveSkill(Guid.NewGuid()));
    }

    /// <summary>
    /// As designações que cumprem o P07 são aceites, incluindo letras acentuadas, e guardadas sem os espaços nas
    /// extremidades (RF009: «C#» é aceite).
    /// </summary>
    /// <param name="label">Designação indicada pelo Candidato.</param>
    /// <param name="expected">Designação guardada.</param>
    [Theory]
    [InlineData("C#", "C#")]
    [InlineData("C++", "C++")]
    [InlineData("Node.js", "Node.js")]
    [InlineData("Gestão de equipas", "Gestão de equipas")]
    [InlineData("Análise de dados", "Análise de dados")]
    [InlineData("Front-end", "Front-end")]
    [InlineData("  Kotlin  ", "Kotlin")]
    public void ValidCustomSkillIsAccepted(string label, string expected)
    {
        Candidate candidate = CreateCandidate();

        CandidateCustomSkill customSkill = candidate.AddCustomSkill(label);

        Assert.NotEqual(Guid.Empty, customSkill.Id);
        Assert.Equal(candidate.Id, customSkill.CandidateId);
        Assert.Equal(expected, customSkill.Label);
        Assert.Same(customSkill, Assert.Single(candidate.CustomSkills));
    }

    /// <summary>
    /// Os limites do P07: 2 e 40 caracteres são aceites.
    /// </summary>
    [Fact]
    public void CustomSkillWithTwoOrFortyCharactersIsAccepted()
    {
        Candidate candidate = CreateCandidate();

        candidate.AddCustomSkill("Go");
        candidate.AddCustomSkill(new string('a', CandidateCustomSkill.MaxLabelLength));

        Assert.Equal(2, candidate.CustomSkills.Count);
    }

    /// <summary>
    /// As designações fora do P07 são rejeitadas com o motivo (RF009: 1 carácter, 41 caracteres ou o carácter «@»). Os
    /// algarismos de outras escritas, como «٣» (U+0663), não contam como algarismos do P07.
    /// </summary>
    /// <param name="label">Designação inválida.</param>
    [Theory]
    [InlineData("C")]
    [InlineData("C@")]
    [InlineData("Java/Kotlin")]
    [InlineData("abc\u0663")]
    [InlineData("\u0663\u0664")]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData(null)]
    public void CustomSkillOutsideP07IsRejected(string? label)
    {
        Candidate candidate = CreateCandidate();

        ArgumentException error = Assert.Throws<ArgumentException>(() => candidate.AddCustomSkill(label!));

        Assert.StartsWith(
            "A competência tem de ter entre 2 e 40 caracteres e conter apenas letras, algarismos, espaços e os símbolos + # . -",
            error.Message,
            StringComparison.Ordinal);
        Assert.Empty(candidate.CustomSkills);
    }

    /// <summary>
    /// Uma designação com 41 caracteres é rejeitada.
    /// </summary>
    [Fact]
    public void CustomSkillWithFortyOneCharactersIsRejected()
    {
        Candidate candidate = CreateCandidate();

        Assert.Throws<ArgumentException>(
            () => candidate.AddCustomSkill(new string('a', CandidateCustomSkill.MaxLabelLength + 1)));
    }

    /// <summary>
    /// A mesma designação «Outro» não pode ser repetida no perfil, sem distinção de maiúsculas.
    /// </summary>
    [Fact]
    public void RepeatedCustomSkillIsRejectedIgnoringCase()
    {
        Candidate candidate = CreateCandidate();
        candidate.AddCustomSkill("Docker");

        Assert.Throws<ArgumentException>(() => candidate.AddCustomSkill("DOCKER"));
        Assert.Single(candidate.CustomSkills);
    }

    /// <summary>
    /// Retirar uma competência «Outro» remove-a e mantém as restantes.
    /// </summary>
    [Fact]
    public void RemoveCustomSkillRemovesOnlyThatSkill()
    {
        Candidate candidate = CreateCandidate();
        CandidateCustomSkill docker = candidate.AddCustomSkill("Docker");
        CandidateCustomSkill kotlin = candidate.AddCustomSkill("Kotlin");

        candidate.RemoveCustomSkill(docker.Id);

        Assert.Same(kotlin, Assert.Single(candidate.CustomSkills));
    }

    /// <summary>
    /// Retirar uma competência «Outro» que não existe no perfil é rejeitado.
    /// </summary>
    [Fact]
    public void RemovingACustomSkillThatDoesNotExistIsRejected()
    {
        Candidate candidate = CreateCandidate();

        Assert.Throws<KeyNotFoundException>(() => candidate.RemoveCustomSkill(Guid.NewGuid()));
    }

    private static Candidate CreateCandidate()
    {
        var user = new AppUser("ana@katch.pt", "hash", UserType.Candidate, Registered, Registered);
        return new Candidate(user, "Ana Silva", "912345678", Guid.NewGuid());
    }
}
