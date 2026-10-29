// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes das competências do perfil em <see cref="CandidateProfileService"/> (documentação da API v02, secções 6.2.4,
/// 6.2.5, 6.2.10 e 6.2.11; RF008, RF009, P07), com repositórios e unidade de trabalho em memória.
/// </summary>
public sealed class CandidateProfileServiceTests
{
    private const string Required = "O campo é obrigatório.";
    private const string NotInList = "O valor não pertence à lista pré-definida.";
    private const string AlreadyAdded = "A competência já está associada ao perfil.";
    private const string LabelFormat =
        "A competência tem de ter entre 2 e 40 caracteres e conter apenas letras, algarismos, espaços e os símbolos + # . -";

    private static readonly Location Guimaraes = new("Guimarães", "Braga", 41.444m, -8.296m);

    private readonly Skill csharp = new("C#");
    private readonly Skill docker = new("Docker");
    private readonly Skill sql = new("SQL");
    private readonly Candidate candidate;
    private readonly FakeCandidates candidates;
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly CandidateProfileService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateProfileServiceTests"/> class.
    /// </summary>
    public CandidateProfileServiceTests()
    {
        DateTimeOffset created = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);
        var user = new AppUser("ana.ferreira@exemplo.pt", "hash", UserType.Candidate, created, created);
        this.candidate = new Candidate(user, "Ana Ferreira", "912345678", Guimaraes.Id);
        this.candidates = new FakeCandidates(this.candidate);
        this.service = new CandidateProfileService(
            this.candidates,
            new FakeReferenceData([this.csharp, this.docker, this.sql]),
            this.unitOfWork);
    }

    /// <summary>
    /// Gets designações da opção «Outro» que cumprem o P07, incluindo os limites de 2 e de 40 caracteres.
    /// </summary>
    public static TheoryData<string> ValidLabels => new()
    {
        "C#",
        "Go",
        new string('a', 40),
        "Node.js",
        "C++ e .NET",
        "Programação",
        "Sistemas-embebidos 2",
        "Análise de dados",
    };

    /// <summary>
    /// Gets designações da opção «Outro» fora do P07: 1 e 41 caracteres, símbolos fora da lista e só espaços.
    /// </summary>
    public static TheoryData<string> InvalidLabels => new()
    {
        "C",
        new string('a', 41),
        "ana@exemplo",
        "C/C++",
        "R&D",
        "abc\u0663",
        "   ",
        "  a  ",
    };

    /// <summary>
    /// Uma competência da lista é associada, gravada uma vez e devolvida no perfil, por ordem alfabética.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddSkillAssociatesTheSkillAndReturnsTheSortedProfile()
    {
        this.candidate.AddSkill(this.sql);

        CandidateProfileDto profile = await this.service.AddSkillAsync(
            this.candidate.Id,
            new AddSkillRequest(this.csharp.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, this.unitOfWork.Saves);
        Assert.Equal([new ReferenceItemDto(this.csharp.Id, "C#"), new ReferenceItemDto(this.sql.Id, "SQL")], profile.Skills);
        Assert.Contains(this.candidate.Skills, skill => skill.Id == this.csharp.Id);
    }

    /// <summary>
    /// Sem <c>skillId</c>, o campo é indicado como obrigatório e nada é gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddSkillWithoutSkillIdIsRequired()
    {
        ValidationFailedException error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            this.service.AddSkillAsync(this.candidate.Id, AddSkillRequest.Empty, TestContext.Current.CancellationToken));

        AssertField(error, "skillId", Required);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Uma competência que não consta da lista pré-definida é recusada (RF008).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddSkillOutsideTheListIsRejected()
    {
        ValidationFailedException error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            this.service.AddSkillAsync(
                this.candidate.Id,
                new AddSkillRequest(Guid.NewGuid()),
                TestContext.Current.CancellationToken));

        AssertField(error, "skillId", NotInList);
        Assert.Empty(this.candidate.Skills);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Uma competência já associada ao perfil é recusada sem gravar.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddSkillAlreadyAssociatedIsRejected()
    {
        this.candidate.AddSkill(this.docker);

        ValidationFailedException error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            this.service.AddSkillAsync(
                this.candidate.Id,
                new AddSkillRequest(this.docker.Id),
                TestContext.Current.CancellationToken));

        AssertField(error, "skillId", AlreadyAdded);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Se outro pedido associou a mesma competência entre a leitura e a gravação, a chave primária de
    /// <c>candidate_skill</c> dá o mesmo erro de campo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrentAddOfTheSameSkillIsReportedAsAlreadyAdded()
    {
        this.unitOfWork.Failure = new UniqueConstraintViolationException("pk_candidate_skill", new InvalidOperationException());

        ValidationFailedException error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            this.service.AddSkillAsync(
                this.candidate.Id,
                new AddSkillRequest(this.csharp.Id),
                TestContext.Current.CancellationToken));

        AssertField(error, "skillId", AlreadyAdded);
    }

    /// <summary>
    /// A competência associada é retirada e a alteração gravada.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RemoveSkillRemovesTheAssociation()
    {
        this.candidate.AddSkill(this.csharp);
        this.candidate.AddSkill(this.sql);

        await this.service.RemoveSkillAsync(this.candidate.Id, this.csharp.Id, TestContext.Current.CancellationToken);

        Assert.Equal(this.sql.Id, Assert.Single(this.candidate.Skills).Id);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Retirar uma competência que não está no perfil dá «não encontrado» (ERR-20), mesmo que exista na lista.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RemoveSkillNotAssociatedIsNotFound()
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(() =>
            this.service.RemoveSkillAsync(this.candidate.Id, this.docker.Id, TestContext.Current.CancellationToken));

        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Se outro pedido retirou a mesma competência entre a leitura e a gravação, o resultado é «não encontrado».
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrentRemovalIsNotFound()
    {
        this.candidate.AddSkill(this.csharp);
        this.unitOfWork.Failure = new ConcurrencyConflictException(new InvalidOperationException());

        await Assert.ThrowsAsync<ElementNotFoundException>(() =>
            this.service.RemoveSkillAsync(this.candidate.Id, this.csharp.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// As designações que cumprem o P07 são aceites, gravadas e devolvidas no perfil (RF009).
    /// </summary>
    /// <param name="label">Designação.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(ValidLabels))]
    public async Task AddCustomSkillAcceptsLabelsWithinP07(string label)
    {
        CandidateProfileDto profile = await this.service.AddCustomSkillAsync(
            this.candidate.Id,
            new AddCustomSkillRequest(label),
            TestContext.Current.CancellationToken);

        CustomSkillDto customSkill = Assert.Single(profile.CustomSkills);
        Assert.Equal(label, customSkill.Label);
        Assert.Equal(Assert.Single(this.candidate.CustomSkills).Id, customSkill.CustomSkillId);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// As designações fora do P07 são recusadas pelo servidor com o motivo do catálogo, sem gravar (RF009).
    /// </summary>
    /// <param name="label">Designação.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(InvalidLabels))]
    public async Task AddCustomSkillRejectsLabelsOutsideP07(string label)
    {
        ValidationFailedException error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            this.service.AddCustomSkillAsync(
                this.candidate.Id,
                new AddCustomSkillRequest(label),
                TestContext.Current.CancellationToken));

        AssertField(error, "label", LabelFormat);
        Assert.Empty(this.candidate.CustomSkills);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Sem <c>label</c>, ou com o texto vazio, o campo é indicado como obrigatório.
    /// </summary>
    /// <param name="label">Designação em falta ou vazia.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AddCustomSkillWithoutLabelIsRequired(string? label)
    {
        ValidationFailedException error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            this.service.AddCustomSkillAsync(
                this.candidate.Id,
                new AddCustomSkillRequest(label),
                TestContext.Current.CancellationToken));

        AssertField(error, "label", Required);
    }

    /// <summary>
    /// A designação é guardada sem os espaços nas extremidades.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddCustomSkillTrimsTheLabel()
    {
        CandidateProfileDto profile = await this.service.AddCustomSkillAsync(
            this.candidate.Id,
            new AddCustomSkillRequest("  Blazor "),
            TestContext.Current.CancellationToken);

        Assert.Equal("Blazor", Assert.Single(profile.CustomSkills).Label);
    }

    /// <summary>
    /// A mesma designação, com outras maiúsculas, é recusada como já indicada no perfil.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddCustomSkillRepeatedWithOtherCaseIsRejected()
    {
        this.candidate.AddCustomSkill("Blazor");

        ValidationFailedException error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            this.service.AddCustomSkillAsync(
                this.candidate.Id,
                new AddCustomSkillRequest("BLAZOR"),
                TestContext.Current.CancellationToken));

        AssertField(error, "label", AlreadyAdded);
        Assert.Single(this.candidate.CustomSkills);
    }

    /// <summary>
    /// Se outro pedido indicou a mesma designação ao mesmo tempo, o índice <c>uq_candidate_custom_skill</c> dá o mesmo
    /// erro de campo.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrentAddOfTheSameLabelIsReportedAsAlreadyAdded()
    {
        this.unitOfWork.Failure = new UniqueConstraintViolationException("uq_candidate_custom_skill", new InvalidOperationException());

        ValidationFailedException error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            this.service.AddCustomSkillAsync(
                this.candidate.Id,
                new AddCustomSkillRequest("Blazor"),
                TestContext.Current.CancellationToken));

        AssertField(error, "label", AlreadyAdded);
    }

    /// <summary>
    /// Se o CHECK <c>ck_candidate_custom_skill_label</c> recusar a designação (a classe <c>[[:alnum:]]</c> depende da
    /// locale da base), o pedido é recusado com o motivo do P07, e não com um erro inesperado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LabelRejectedByTheDatabaseCheckIsReportedAsFormatError()
    {
        this.unitOfWork.Failure = new CheckConstraintViolationException("ck_candidate_custom_skill_label", new InvalidOperationException());

        ValidationFailedException error = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            this.service.AddCustomSkillAsync(
                this.candidate.Id,
                new AddCustomSkillRequest("Gestão de equipas"),
                TestContext.Current.CancellationToken));

        AssertField(error, "label", LabelFormat);
    }

    /// <summary>
    /// A violação de outro CHECK não é confundida com a designação e chega ao filtro global sem alteração.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OtherCheckViolationIsNotConverted()
    {
        this.unitOfWork.Failure = new CheckConstraintViolationException("ck_outra_restricao", new InvalidOperationException());

        CheckConstraintViolationException error = await Assert.ThrowsAsync<CheckConstraintViolationException>(() =>
            this.service.AddCustomSkillAsync(
                this.candidate.Id,
                new AddCustomSkillRequest("Blazor"),
                TestContext.Current.CancellationToken));

        Assert.Equal("ck_outra_restricao", error.ConstraintName);
    }

    /// <summary>
    /// A competência «Outro» é retirada e a alteração gravada; a que não existe dá «não encontrado».
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RemoveCustomSkillRemovesOnlyExistingOnes()
    {
        CandidateCustomSkill blazor = this.candidate.AddCustomSkill("Blazor");

        await this.service.RemoveCustomSkillAsync(this.candidate.Id, blazor.Id, TestContext.Current.CancellationToken);

        Assert.Empty(this.candidate.CustomSkills);
        await Assert.ThrowsAsync<ElementNotFoundException>(() =>
            this.service.RemoveCustomSkillAsync(this.candidate.Id, blazor.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Um Candidato inexistente dá «não encontrado» nas quatro operações.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownCandidateIsNotFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid unknown = Guid.NewGuid();

        await Assert.ThrowsAsync<ElementNotFoundException>(() =>
            this.service.AddSkillAsync(unknown, new AddSkillRequest(this.csharp.Id), cancellationToken));
        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.RemoveSkillAsync(unknown, this.csharp.Id, cancellationToken));
        await Assert.ThrowsAsync<ElementNotFoundException>(() =>
            this.service.AddCustomSkillAsync(unknown, new AddCustomSkillRequest("Blazor"), cancellationToken));
        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.RemoveCustomSkillAsync(unknown, Guid.NewGuid(), cancellationToken));
    }

    /// <summary>
    /// O perfil devolvido tem os dados de registo, a localidade «Localidade (Distrito)», as hiperligações por ordem, o
    /// indicador do CV e as preferências; sem fotografia gravada, <c>photoUrl</c> fica vazio.
    /// </summary>
    [Fact]
    public void ProfileDtoCarriesTheRegistrationDataAndThePreferences()
    {
        this.candidate.DesiredRole = "Programadora backend";
        this.candidate.Availability = Availability.OneMonth;
        this.candidate.CvPath = "cv/ana.pdf";
        this.candidate.MaxDistanceKm = 50;
        this.candidate.MinSalaryExpectation = 1400m;
        this.candidate.PreferredWorkModes = [WorkMode.Hybrid, WorkMode.Remote];
        this.candidate.SetLinks(["https://github.com/ana", "https://ana.pt"]);
        this.candidate.AddCustomSkill("Entity Framework");
        this.candidate.AddCustomSkill("Blazor");

        CandidateProfileDto profile = CandidateProfileService.ToDto(this.candidate, Guimaraes);

        Assert.Equal(this.candidate.Id, profile.CandidateId);
        Assert.Equal("ana.ferreira@exemplo.pt", profile.Email);
        Assert.Equal("Guimarães (Braga)", profile.LocationName);
        Assert.Equal(Availability.OneMonth, profile.Availability);
        Assert.True(profile.HasCv);
        Assert.Null(profile.PhotoUrl);
        Assert.Equal(["https://github.com/ana", "https://ana.pt"], profile.Links);
        Assert.Equal(["Blazor", "Entity Framework"], profile.CustomSkills.Select(item => item.Label));
        Assert.Equal(50, profile.Preferences.MaxDistanceKm);
        Assert.Equal(1400m, profile.Preferences.MinSalaryExpectation);
        Assert.Equal([WorkMode.Hybrid, WorkMode.Remote], profile.Preferences.WorkModes);
        Assert.Empty(profile.Preferences.ContractTypes);
    }

    private static void AssertField(ValidationFailedException error, string field, string reason)
    {
        FieldErrorDto fieldError = Assert.Single(error.Fields);
        Assert.Equal(field, fieldError.Field);
        Assert.Equal(reason, fieldError.Reason);
    }

    private sealed class FakeCandidates : ICandidateRepository
    {
        private readonly Candidate candidate;

        public FakeCandidates(Candidate candidate)
        {
            this.candidate = candidate;
        }

        public Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Candidate?> GetWithProfileAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == this.candidate.Id ? this.candidate : null);
        }

        public Task<List<Candidate>> ListQuotaDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeReferenceData : IReferenceDataRepository
    {
        private readonly List<Skill> skills;

        public FakeReferenceData(List<Skill> skills)
        {
            this.skills = skills;
        }

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Location?> GetLocationAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == Guimaraes.Id ? Guimaraes : null);
        }

        public Task<List<Skill>> ListSkillsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.skills.ToList());
        }

        public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.skills.Find(skill => skill.Id == id));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public Exception? Failure { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (this.Failure is not null)
            {
                throw this.Failure;
            }

            this.Saves++;
            return Task.FromResult(1);
        }

        public void DiscardChanges()
        {
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
