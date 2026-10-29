// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes de <see cref="ReferenceListService"/>: consulta das localidades e dos benefícios e manutenção das competências
/// e dos benefícios pelo Administrador (documentação da API v02, secções 6.13.1 e 6.13.3 a 6.13.7; RF099 a RF102,
/// RF112).
/// </summary>
public sealed class ReferenceListServiceTests
{
    private static readonly Guid AdminId = Guid.NewGuid();

    private readonly FakeReferenceData referenceData = new();
    private readonly FakeOperationLogger operationLogger = new();
    private readonly FakeUnitOfWork unitOfWork = new();

    /// <summary>
    /// Gets the empty or too long names (above 100 characters), with the reason expected in the <c>name</c> field.
    /// </summary>
    public static TheoryData<string?, string> InvalidNames => new()
    {
        { null, FieldReasons.ReferenceNameRequired },
        { string.Empty, FieldReasons.ReferenceNameRequired },
        { "   ", FieldReasons.ReferenceNameRequired },
        { new string('a', 101), FieldReasons.TextTooLong(100) },
        { "  " + new string('a', 101) + "  ", FieldReasons.TextTooLong(100) },
    };

    /// <summary>
    /// As localidades vêm como «Localidade (Distrito)», por ordem alfabética portuguesa e sem coordenadas.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LocationsAreNamedWithDistrictAndSortedAlphabetically()
    {
        var evora = new Location("Évora", "Évora", 38.571m, -7.909m);
        var guimaraes = new Location("Guimarães", "Braga", 41.444m, -8.296m);
        var braga = new Location("Braga", "Braga", 41.545m, -8.426m);
        var esposende = new Location("Esposende", "Braga", 41.532m, -8.781m);
        this.referenceData.Locations.AddRange([guimaraes, evora, braga, esposende]);

        List<ReferenceItemDto> items = await this.Service().ListLocationsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new ReferenceItemDto(braga.Id, "Braga (Braga)"),
                new ReferenceItemDto(esposende.Id, "Esposende (Braga)"),
                new ReferenceItemDto(evora.Id, "Évora (Évora)"),
                new ReferenceItemDto(guimaraes.Id, "Guimarães (Braga)"),
            ],
            items);
    }

    /// <summary>
    /// Duas localidades com o mesmo nome distinguem-se pelo distrito (DAPI-08).
    /// </summary>
    [Fact]
    public void DisplayNameIncludesTheDistrict()
    {
        Assert.Equal("Almada (Setúbal)", ReferenceListService.DisplayName(new Location("Almada", "Setúbal", 38.68m, -9.16m)));
    }

    /// <summary>
    /// Os benefícios vêm por ordem alfabética portuguesa, sem distinção de maiúsculas e com os acentos junto da letra
    /// (6.13.3).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task BenefitsAreSortedAlphabetically()
    {
        var health = new Benefit("Seguro de saúde");
        var training = new Benefit("Ações de formação");
        var flexible = new Benefit("horário flexível");
        var car = new Benefit("Carro de empresa");
        this.referenceData.Benefits.AddRange([health, training, flexible, car]);

        List<ReferenceItemDto> items = await this.Service().ListBenefitsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new ReferenceItemDto(training.Id, "Ações de formação"),
                new ReferenceItemDto(car.Id, "Carro de empresa"),
                new ReferenceItemDto(flexible.Id, "horário flexível"),
                new ReferenceItemDto(health.Id, "Seguro de saúde"),
            ],
            items);
    }

    /// <summary>
    /// Sem benefícios, a lista é vazia e não nula (documentação da API v02, secção 2.3).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmptyBenefitListIsEmpty()
    {
        Assert.Empty(await this.Service().ListBenefitsAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A competência nova fica com a designação sem os espaços nas extremidades, é gravada e a criação é registada com o
    /// Administrador (RF099, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddSkillCreatesTheSkillAndLogsTheOperation()
    {
        ReferenceItemDto created = await this.Service().AddSkillAsync(
            AdminId,
            new ReferenceItemRequest("  Kubernetes  "),
            TestContext.Current.CancellationToken);

        Skill skill = Assert.Single(this.referenceData.Skills);
        Assert.Equal(new ReferenceItemDto(skill.Id, "Kubernetes"), created);
        Assert.Equal([(AdminId, OperationType.SkillCreated, skill.Id)], this.operationLogger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// O benefício novo é gravado e a criação é registada com o Administrador (RF101, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AddBenefitCreatesTheBenefitAndLogsTheOperation()
    {
        ReferenceItemDto created = await this.Service().AddBenefitAsync(
            AdminId,
            new ReferenceItemRequest("Horário flexível"),
            TestContext.Current.CancellationToken);

        Benefit benefit = Assert.Single(this.referenceData.Benefits);
        Assert.Equal(new ReferenceItemDto(benefit.Id, "Horário flexível"), created);
        Assert.Equal([(AdminId, OperationType.BenefitCreated, benefit.Id)], this.operationLogger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Uma designação com exatamente 100 caracteres é aceite (limite do DM-12).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task NameWithMaximumLengthIsAccepted()
    {
        string name = new('a', 100);

        ReferenceItemDto created = await this.Service().AddSkillAsync(
            AdminId,
            new ReferenceItemRequest(name),
            TestContext.Current.CancellationToken);

        Assert.Equal(name, created.Name);
    }

    /// <summary>
    /// Uma competência com a designação vazia ou demasiado longa é recusada com o campo <c>name</c>, sem gravar nem
    /// registar nada (RF099).
    /// </summary>
    /// <param name="name">Designação enviada.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task InvalidSkillNameIsRejected(string? name, string reason)
    {
        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().AddSkillAsync(AdminId, new ReferenceItemRequest(name), TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("name", reason)], exception.Fields);
        this.AssertNothingChanged();
    }

    /// <summary>
    /// Um benefício com a designação vazia ou demasiado longa é recusado com o campo <c>name</c> (RF101).
    /// </summary>
    /// <param name="name">Designação enviada.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task InvalidBenefitNameIsRejected(string? name, string reason)
    {
        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().AddBenefitAsync(AdminId, new ReferenceItemRequest(name), TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("name", reason)], exception.Fields);
        this.AssertNothingChanged();
    }

    /// <summary>
    /// Uma competência que já existe com outras maiúsculas é recusada (RF099).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DuplicateSkillIgnoringCaseIsRejected()
    {
        this.referenceData.Skills.Add(new Skill("Kubernetes"));

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().AddSkillAsync(AdminId, new ReferenceItemRequest(" KUBERNETES "), TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("name", FieldReasons.ReferenceNameTaken)], exception.Fields);
        Assert.Single(this.referenceData.Skills);
        this.AssertNothingChanged();
    }

    /// <summary>
    /// Um benefício que já existe com outras maiúsculas é recusado (RF101).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DuplicateBenefitIgnoringCaseIsRejected()
    {
        this.referenceData.Benefits.Add(new Benefit("Seguro de saúde"));

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().AddBenefitAsync(AdminId, new ReferenceItemRequest("seguro de SAÚDE"), TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("name", FieldReasons.ReferenceNameTaken)], exception.Fields);
        this.AssertNothingChanged();
    }

    /// <summary>
    /// A alteração muda só a designação: o identificador, de que dependem as associações a perfis e vagas, mantém-se, e a
    /// alteração é registada com o Administrador (RF100, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenameSkillKeepsTheIdentifierAndLogsTheOperation()
    {
        var skill = new Skill("Docker");
        this.referenceData.Skills.Add(skill);

        ReferenceItemDto renamed = await this.Service().RenameSkillAsync(
            AdminId,
            skill.Id,
            new ReferenceItemRequest(" Docker Compose "),
            TestContext.Current.CancellationToken);

        Assert.Equal(new ReferenceItemDto(skill.Id, "Docker Compose"), renamed);
        Assert.Equal("Docker Compose", skill.Name);
        Assert.Equal([(AdminId, OperationType.SkillRenamed, skill.Id)], this.operationLogger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Mudar só as maiúsculas da própria competência é permitido: a verificação ignora a própria competência.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenameSkillToADifferentCaseOfItsOwnNameIsAccepted()
    {
        var skill = new Skill("sql");
        this.referenceData.Skills.Add(skill);

        ReferenceItemDto renamed = await this.Service().RenameSkillAsync(
            AdminId,
            skill.Id,
            new ReferenceItemRequest("SQL"),
            TestContext.Current.CancellationToken);

        Assert.Equal("SQL", renamed.Name);
        Assert.Equal(skill.Id, this.referenceData.LastExceptId);
    }

    /// <summary>
    /// A designação de outra competência, com outras maiúsculas, é recusada e a competência fica como estava (RF100;
    /// exemplo de 6.13.5).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenameSkillToAnotherSkillNameIsRejected()
    {
        var csharp = new Skill("C#");
        var sql = new Skill("SQL");
        this.referenceData.Skills.AddRange([csharp, sql]);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().RenameSkillAsync(AdminId, sql.Id, new ReferenceItemRequest("c#"), TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("name", FieldReasons.ReferenceNameTaken)], exception.Fields);
        Assert.Equal("SQL", sql.Name);
        this.AssertNothingChanged();
    }

    /// <summary>
    /// A alteração com a designação vazia é recusada antes de mudar a competência (RF100).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenameSkillToAnEmptyNameIsRejected()
    {
        var skill = new Skill("Docker");
        this.referenceData.Skills.Add(skill);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().RenameSkillAsync(AdminId, skill.Id, new ReferenceItemRequest(" "), TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("name", FieldReasons.ReferenceNameRequired)], exception.Fields);
        Assert.Equal("Docker", skill.Name);
        this.AssertNothingChanged();
    }

    /// <summary>
    /// Uma competência inexistente dá <see cref="ElementNotFoundException"/> (404, ERR-20), mesmo com a designação
    /// inválida.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenameUnknownSkillIsNotFound()
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.Service().RenameSkillAsync(AdminId, Guid.NewGuid(), new ReferenceItemRequest(null), TestContext.Current.CancellationToken));

        this.AssertNothingChanged();
    }

    /// <summary>
    /// A alteração do benefício mantém o identificador e é registada com o Administrador (RF102, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenameBenefitKeepsTheIdentifierAndLogsTheOperation()
    {
        var benefit = new Benefit("Seguro de saúde");
        this.referenceData.Benefits.Add(benefit);

        ReferenceItemDto renamed = await this.Service().RenameBenefitAsync(
            AdminId,
            benefit.Id,
            new ReferenceItemRequest("Seguro de saúde extensível ao agregado"),
            TestContext.Current.CancellationToken);

        Assert.Equal(new ReferenceItemDto(benefit.Id, "Seguro de saúde extensível ao agregado"), renamed);
        Assert.Equal([(AdminId, OperationType.BenefitRenamed, benefit.Id)], this.operationLogger.Entries);
        Assert.Equal(benefit.Id, this.referenceData.LastExceptId);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// A designação de outro benefício é recusada (RF102).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenameBenefitToAnotherBenefitNameIsRejected()
    {
        var health = new Benefit("Seguro de saúde");
        var car = new Benefit("Carro de empresa");
        this.referenceData.Benefits.AddRange([health, car]);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.Service().RenameBenefitAsync(AdminId, car.Id, new ReferenceItemRequest("SEGURO DE SAÚDE"), TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("name", FieldReasons.ReferenceNameTaken)], exception.Fields);
        Assert.Equal("Carro de empresa", car.Name);
        this.AssertNothingChanged();
    }

    /// <summary>
    /// Um benefício inexistente dá <see cref="ElementNotFoundException"/> (404, ERR-20).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RenameUnknownBenefitIsNotFound()
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.Service().RenameBenefitAsync(AdminId, Guid.NewGuid(), new ReferenceItemRequest("Ginásio"), TestContext.Current.CancellationToken));

        this.AssertNothingChanged();
    }

    /// <summary>
    /// Dois pedidos concorrentes com a mesma designação: o segundo viola o índice único e recebe a mesma resposta que a
    /// designação repetida.
    /// </summary>
    /// <param name="isSkill">Competência (<see langword="true"/>) ou benefício.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentDuplicateIsRejectedAsTaken(bool isSkill)
    {
        string index = isSkill ? ReferenceListService.SkillNameUniqueIndex : ReferenceListService.BenefitNameUniqueIndex;
        this.unitOfWork.FailWith = new UniqueConstraintViolationException(index, new InvalidOperationException());
        var request = new ReferenceItemRequest("Kotlin");

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => isSkill
                ? this.Service().AddSkillAsync(AdminId, request, TestContext.Current.CancellationToken)
                : this.Service().AddBenefitAsync(AdminId, request, TestContext.Current.CancellationToken));

        Assert.Equal([new FieldErrorDto("name", FieldReasons.ReferenceNameTaken)], exception.Fields);
    }

    /// <summary>
    /// A violação de outro índice não é confundida com a designação repetida.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OtherUniqueViolationIsNotTranslated()
    {
        this.unitOfWork.FailWith = new UniqueConstraintViolationException(
            ReferenceListService.BenefitNameUniqueIndex,
            new InvalidOperationException());

        await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => this.Service().AddSkillAsync(AdminId, new ReferenceItemRequest("Kotlin"), TestContext.Current.CancellationToken));
    }

    private ReferenceListService Service()
    {
        return new ReferenceListService(this.referenceData, this.operationLogger, this.unitOfWork);
    }

    private void AssertNothingChanged()
    {
        Assert.Empty(this.operationLogger.Entries);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    private sealed class FakeReferenceData : IReferenceDataRepository
    {
        public List<Location> Locations { get; } = [];

        public List<Skill> Skills { get; } = [];

        public List<Benefit> Benefits { get; } = [];

        public Guid? LastExceptId { get; private set; }

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Locations.ToList());
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Locations.Any(location => location.Id == id));
        }

        public Task<List<Benefit>> ListBenefitsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Benefits.ToList());
        }

        public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Skills.Find(skill => skill.Id == id));
        }

        public Task<Benefit?> GetBenefitAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Benefits.Find(benefit => benefit.Id == id));
        }

        public Task<bool> SkillNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            this.LastExceptId = exceptId;
            return Task.FromResult(this.Skills.Exists(skill => SameName(skill.Name, name) && skill.Id != exceptId));
        }

        public Task<bool> BenefitNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            this.LastExceptId = exceptId;
            return Task.FromResult(this.Benefits.Exists(benefit => SameName(benefit.Name, name) && benefit.Id != exceptId));
        }

        public Task AddSkillAsync(Skill skill, CancellationToken cancellationToken = default)
        {
            this.Skills.Add(skill);
            return Task.CompletedTask;
        }

        public Task AddBenefitAsync(Benefit benefit, CancellationToken cancellationToken = default)
        {
            this.Benefits.Add(benefit);
            return Task.CompletedTask;
        }

        private static bool SameName(string stored, string requested)
        {
            return string.Equals(stored, requested, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class FakeOperationLogger : IOperationLogger
    {
        public List<(Guid? UserId, OperationType Operation, Guid EntityId)> Entries { get; } = [];

        public Task LogAsync(
            Guid? userId,
            OperationType operation,
            Guid entityId,
            CancellationToken cancellationToken = default)
        {
            this.Entries.Add((userId, operation, entityId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public Exception? FailWith { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (this.FailWith is not null)
            {
                throw this.FailWith;
            }

            this.Saves++;
            return Task.FromResult(1);
        }

        public void DiscardChanges()
        {
            throw new NotSupportedException();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
