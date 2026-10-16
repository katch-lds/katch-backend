// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="Candidate"/>: criação, quota de interesses (RF020 a RF022, P11) e hiperligações
/// profissionais (RF012, P10).
/// </summary>
public sealed class CandidateTests
{
    private static readonly DateTimeOffset Registered = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private static readonly Guid LocationId = Guid.NewGuid();

    /// <summary>
    /// Um Candidato novo partilha o identificador da conta, tem a quota completa e as preferências vazias.
    /// </summary>
    [Fact]
    public void NewCandidateHasFullQuotaAndEmptyPreferences()
    {
        AppUser user = CreateUser();

        var candidate = new Candidate(user, "Ana Silva", "912345678", LocationId);

        Assert.Equal(user.Id, candidate.Id);
        Assert.Same(user, candidate.User);
        Assert.Equal("Ana Silva", candidate.FullName);
        Assert.Equal("912345678", candidate.PhoneNumber);
        Assert.Equal(LocationId, candidate.LocationId);
        Assert.Equal(10, candidate.SwipeRightsRemaining);
        Assert.Null(candidate.NextSwipeAvailableAt);
        Assert.Empty(candidate.PreferredWorkModes);
        Assert.Empty(candidate.PreferredContractTypes);
        Assert.Empty(candidate.Links);
        Assert.Null(candidate.MaxDistanceKm);
    }

    /// <summary>
    /// Só uma conta do tipo Candidato pode ser especializada em Candidato.
    /// </summary>
    /// <param name="userType">Tipo de conta que não é de Candidato.</param>
    [Theory]
    [InlineData(UserType.Recruiter)]
    [InlineData(UserType.Admin)]
    public void AccountOfAnotherTypeIsRejected(UserType userType)
    {
        DateTimeOffset? terms = userType == UserType.Admin ? null : Registered;
        var user = new AppUser("outro@katch.pt", "hash", userType, terms, Registered);

        Assert.Throws<ArgumentException>(() => new Candidate(user, "Ana Silva", "912345678", LocationId));
    }

    /// <summary>
    /// O nome e o telefone obrigatórios têm de respeitar as regras do modelo de dados.
    /// </summary>
    /// <param name="fullName">Nome do Candidato.</param>
    /// <param name="phoneNumber">Telefone do Candidato.</param>
    [Theory]
    [InlineData("", "912345678")]
    [InlineData("   ", "912345678")]
    [InlineData("Ana Silva", "91234567")]
    [InlineData("Ana Silva", "9123456789")]
    [InlineData("Ana Silva", "91234567a")]
    [InlineData("Ana Silva", "")]
    public void InvalidNameOrPhoneIsRejected(string fullName, string phoneNumber)
    {
        AppUser user = CreateUser();

        Assert.ThrowsAny<ArgumentException>(() => new Candidate(user, fullName, phoneNumber, LocationId));
    }

    /// <summary>
    /// O nome com mais de 150 caracteres é rejeitado.
    /// </summary>
    [Fact]
    public void NameLongerThanTheColumnIsRejected()
    {
        AppUser user = CreateUser();

        Assert.ThrowsAny<ArgumentException>(() => new Candidate(user, new string('a', 151), "912345678", LocationId));
    }

    /// <summary>
    /// Cada interesse consome um da quota, sem iniciar bloqueio enquanto restarem interesses.
    /// </summary>
    [Fact]
    public void ConsumeInterestDecrementsTheQuota()
    {
        Candidate candidate = CreateCandidate();

        candidate.ConsumeInterest(Registered);
        candidate.ConsumeInterest(Registered);
        candidate.ConsumeInterest(Registered);

        Assert.Equal(7, candidate.SwipeRightsRemaining);
        Assert.Null(candidate.NextSwipeAvailableAt);
        Assert.True(candidate.HasInterestAvailable(Registered));
    }

    /// <summary>
    /// O interesse que esgota a quota inicia o bloqueio de 24 horas contadas desse interesse.
    /// </summary>
    [Fact]
    public void LastInterestStartsTheBlockPeriod()
    {
        Candidate candidate = CreateCandidate();

        ConsumeAll(candidate, Registered);

        Assert.Equal(0, candidate.SwipeRightsRemaining);
        Assert.Equal(Registered.AddHours(24), candidate.NextSwipeAvailableAt);
        Assert.False(candidate.HasInterestAvailable(Registered.AddHours(23)));
    }

    /// <summary>
    /// Com a quota esgotada há menos de 24 horas um novo interesse é rejeitado e a quota não é alterada.
    /// </summary>
    [Fact]
    public void ConsumeInterestIsRejectedDuringTheBlockPeriod()
    {
        Candidate candidate = CreateCandidate();
        ConsumeAll(candidate, Registered);
        DateTimeOffset almost = Registered.AddHours(24).AddTicks(-1);

        Assert.Throws<InvalidOperationException>(() => candidate.ConsumeInterest(almost));

        Assert.Equal(0, candidate.SwipeRightsRemaining);
        Assert.Equal(Registered.AddHours(24), candidate.NextSwipeAvailableAt);
    }

    /// <summary>
    /// Decorridas 24 horas sobre o último interesse, o interesse seguinte é aceite com a quota reposta na totalidade.
    /// </summary>
    [Fact]
    public void ConsumeInterestAfterTheBlockPeriodRestoresTheQuotaFirst()
    {
        Candidate candidate = CreateCandidate();
        ConsumeAll(candidate, Registered);
        DateTimeOffset afterBlock = Registered.AddHours(24);

        Assert.True(candidate.HasInterestAvailable(afterBlock));
        candidate.ConsumeInterest(afterBlock);

        Assert.Equal(9, candidate.SwipeRightsRemaining);
        Assert.Null(candidate.NextSwipeAvailableAt);
    }

    /// <summary>
    /// A reposição acontece só no fim do período e é sempre integral.
    /// </summary>
    [Fact]
    public void RestoreQuotaIfDueRestoresOnlyAfterTheBlockPeriod()
    {
        Candidate candidate = CreateCandidate();
        ConsumeAll(candidate, Registered);

        Assert.False(candidate.RestoreQuotaIfDue(Registered.AddHours(23)));
        Assert.Equal(0, candidate.SwipeRightsRemaining);

        Assert.True(candidate.RestoreQuotaIfDue(Registered.AddHours(24)));
        Assert.Equal(10, candidate.SwipeRightsRemaining);
        Assert.Null(candidate.NextSwipeAvailableAt);
        Assert.False(candidate.RestoreQuotaIfDue(Registered.AddHours(48)));
    }

    /// <summary>
    /// Com interesses consumidos mas a quota não esgotada, o decurso do tempo não repõe a quota.
    /// </summary>
    [Fact]
    public void QuotaIsNotRestoredBeforeBeingExhausted()
    {
        Candidate candidate = CreateCandidate();
        candidate.ConsumeInterest(Registered);
        candidate.ConsumeInterest(Registered);
        candidate.ConsumeInterest(Registered);

        Assert.False(candidate.RestoreQuotaIfDue(Registered.AddDays(30)));
        Assert.Equal(7, candidate.SwipeRightsRemaining);
    }

    /// <summary>
    /// As hiperligações ficam nas posições 1 a 3 pela ordem indicada.
    /// </summary>
    [Fact]
    public void SetLinksAssignsPositionsInOrder()
    {
        Candidate candidate = CreateCandidate();

        candidate.SetLinks(["https://github.com/ana", "http://ana.pt", "HTTPS://linkedin.com/in/ana"]);

        Assert.Equal([1, 2, 3], candidate.Links.Select(link => (int)link.Position).ToArray());
        Assert.Equal(
            ["https://github.com/ana", "http://ana.pt", "HTTPS://linkedin.com/in/ana"],
            candidate.Links.Select(link => link.Url).ToArray());
        Assert.All(candidate.Links, link => Assert.Equal(candidate.Id, link.CandidateId));
    }

    /// <summary>
    /// Substituir as hiperligações atualiza as existentes na própria posição, acrescenta as novas e remove as que sobram.
    /// </summary>
    [Fact]
    public void SetLinksReplacesTheExistingLinks()
    {
        Candidate candidate = CreateCandidate();
        candidate.SetLinks(["https://a.pt", "https://b.pt", "https://c.pt"]);
        CandidateLink first = candidate.Links.First();

        candidate.SetLinks(["https://x.pt"]);

        CandidateLink only = Assert.Single(candidate.Links);
        Assert.Same(first, only);
        Assert.Equal("https://x.pt", only.Url);
        Assert.Equal(1, only.Position);

        candidate.SetLinks([]);
        Assert.Empty(candidate.Links);
    }

    /// <summary>
    /// Mais de três hiperligações, repetidas ou com formato inválido são rejeitadas sem alterar as existentes.
    /// </summary>
    /// <param name="first">Primeiro endereço.</param>
    /// <param name="second">Segundo endereço.</param>
    [Theory]
    [InlineData("https://a.pt", "https://a.pt")]
    [InlineData("ftp://a.pt", "https://b.pt")]
    [InlineData("a.pt", "https://b.pt")]
    [InlineData("https://a.pt", "")]
    public void InvalidLinksAreRejectedAndKeepTheExistingOnes(string first, string second)
    {
        Candidate candidate = CreateCandidate();
        candidate.SetLinks(["https://original.pt"]);

        Assert.Throws<ArgumentException>(() => candidate.SetLinks([first, second]));

        Assert.Equal("https://original.pt", Assert.Single(candidate.Links).Url);
    }

    /// <summary>
    /// Mais de três hiperligações são rejeitadas, tal como endereços com mais de 500 caracteres.
    /// </summary>
    [Fact]
    public void MoreThanThreeOrTooLongLinksAreRejected()
    {
        Candidate candidate = CreateCandidate();

        Assert.Throws<ArgumentException>(
            () => candidate.SetLinks(["https://a.pt", "https://b.pt", "https://c.pt", "https://d.pt"]));
        Assert.Throws<ArgumentException>(() => candidate.SetLinks(["https://" + new string('a', 493)]));
        Assert.Empty(candidate.Links);
    }

    private static AppUser CreateUser()
    {
        return new AppUser("candidato@katch.pt", "hash", UserType.Candidate, Registered, Registered);
    }

    private static Candidate CreateCandidate()
    {
        return new Candidate(CreateUser(), "Ana Silva", "912345678", LocationId);
    }

    private static void ConsumeAll(Candidate candidate, DateTimeOffset now)
    {
        for (int index = 0; index < Candidate.MaxInterests; index++)
        {
            candidate.ConsumeInterest(now);
        }
    }
}
