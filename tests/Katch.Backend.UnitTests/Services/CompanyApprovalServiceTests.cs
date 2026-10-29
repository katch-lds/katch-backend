// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes da consulta das Empresas pendentes e dos dados submetidos, da aprovação e da recusa do registo em
/// <see cref="CompanyAdminService"/> (documentação da API v02, secções 6.11.1 a 6.11.4; RF091 a RF094, RF112, P12), com
/// repositórios em memória.
/// </summary>
public sealed class CompanyApprovalServiceTests
{
    private const string ValidReason = "A morada não corresponde à certidão.";

    private static readonly DateTimeOffset Created = new(2026, 10, 9, 14, 30, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.Parse("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c01");

    private static long taxIdCounter = 509_100_000;

    private readonly FakeCompanies companies = new();
    private readonly FakeReferenceData referenceData = new();
    private readonly FakeOperationLogger logger = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly CompanyAdminService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyApprovalServiceTests"/> class.
    /// </summary>
    public CompanyApprovalServiceTests()
    {
        this.service = new CompanyAdminService(this.companies, this.referenceData, this.logger, this.unitOfWork);
    }

    /// <summary>
    /// A lista tem só as Empresas pendentes, com a designação, o NIF e a data de submissão, da submissão mais antiga para
    /// a mais recente (RF091).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListPendingReturnsOnlyPendingCompaniesFromTheOldestSubmission()
    {
        Company newer = this.AddCompany("Zeta Logística, S.A.", CompanyStatus.Pending, Created.AddDays(2));
        Company older = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Pending, Created);
        this.AddCompany("Empresa Aprovada, Lda.", CompanyStatus.Approved, Created.AddDays(-5));
        this.AddCompany("Empresa Recusada, Lda.", CompanyStatus.Rejected, Created.AddDays(-4));
        this.AddCompany("Empresa Suspensa, Lda.", CompanyStatus.Suspended, Created.AddDays(-3));

        List<PendingCompanyDto> list = await this.service.ListPendingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new PendingCompanyDto(older.Id, "Lumen Software, Lda.", older.TaxId, Created),
                new PendingCompanyDto(newer.Id, "Zeta Logística, S.A.", newer.TaxId, Created.AddDays(2)),
            ],
            list);
    }

    /// <summary>
    /// Duas Empresas submetidas no mesmo instante ficam ordenadas pelo identificador.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListPendingBreaksTiesByCompanyId()
    {
        Company first = this.AddCompany("Empresa A, Lda.", CompanyStatus.Pending, Created);
        Company second = this.AddCompany("Empresa B, Lda.", CompanyStatus.Pending, Created);

        List<PendingCompanyDto> list = await this.service.ListPendingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { first.Id, second.Id }.Order(), list.Select(item => item.CompanyId));
    }

    /// <summary>
    /// Sem Empresas pendentes a lista é vazia.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ListPendingIsEmptyWithoutPendingCompanies()
    {
        this.AddCompany("Empresa Aprovada, Lda.", CompanyStatus.Approved, Created);

        Assert.Empty(await this.service.ListPendingAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Os dados submetidos têm os onze campos, com a localidade no formato «Localidade (Distrito)», em qualquer estado
    /// da Empresa (RF092).
    /// </summary>
    /// <param name="status">Estado da Empresa.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending)]
    [InlineData(CompanyStatus.Approved)]
    [InlineData(CompanyStatus.Rejected)]
    [InlineData(CompanyStatus.Suspended)]
    public async Task GetSubmittedDataReturnsEveryFieldInAnyStatus(CompanyStatus status)
    {
        Company company = this.AddCompany("Lumen Software, Lda.", status, Created);

        CompanyRegistrationDto data = await this.service.GetSubmittedDataAsync(company.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            new CompanyRegistrationDto(
                company.Id,
                "Lumen Software, Lda.",
                company.TaxId,
                Industry.Technology,
                "Avenida da Liberdade, 100, 4710-000 Braga",
                this.referenceData.Braga.Id,
                "Braga (Braga)",
                "geral@exemplo.pt",
                "253000111",
                "Rui Matos",
                Created),
            data);
    }

    /// <summary>
    /// Uma Empresa inexistente dá <c>404</c> (ERR-20) na consulta, na aprovação e na recusa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownCompanyIsNotFound()
    {
        Guid unknown = Guid.NewGuid();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.GetSubmittedDataAsync(unknown, cancellationToken));
        await Assert.ThrowsAsync<ElementNotFoundException>(() => this.service.ApproveAsync(AdminId, unknown, cancellationToken));
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.RejectAsync(AdminId, unknown, new RejectCompanyRequest(ValidReason), cancellationToken));
        this.AssertNothingSaved();
    }

    /// <summary>
    /// A aprovação passa a Empresa pendente a aprovada e regista <c>COMPANY_APPROVED</c> com o Administrador, numa única
    /// gravação (RF093, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ApproveApprovesThePendingCompanyAndLogsTheOperation()
    {
        Company company = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Pending, Created);

        await this.service.ApproveAsync(AdminId, company.Id, TestContext.Current.CancellationToken);

        Assert.Equal(CompanyStatus.Approved, company.Status);
        Assert.Equal([(AdminId, OperationType.CompanyApproved, company.Id)], this.logger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Só uma Empresa pendente pode ser aprovada: <c>409</c> (ERR-30) com o estado atual e nada é gravado.
    /// </summary>
    /// <param name="status">Estado atual da Empresa.</param>
    /// <param name="label">Designação do estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Approved, "aprovada")]
    [InlineData(CompanyStatus.Rejected, "recusada")]
    [InlineData(CompanyStatus.Suspended, "suspensa")]
    public async Task ApprovingACompanyThatIsNotPendingIsAnInvalidState(CompanyStatus status, string label)
    {
        Company company = this.AddCompany("Lumen Software, Lda.", status, Created);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.ApproveAsync(AdminId, company.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-30", exception.Error.Code);
        Assert.Equal($"A operação não é permitida no estado atual: {label}.", exception.Error.Message);
        Assert.Equal(status, company.Status);
        this.AssertNothingSaved();
    }

    /// <summary>
    /// Se a Empresa for alterada por outro pedido entre a leitura e a gravação, a aprovação é recusada com <c>409</c>
    /// (ERR-41) e nada fica gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrentChangeWhileApprovingIsAConflict()
    {
        Company company = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Pending, Created);
        this.unitOfWork.FailWith = new ConcurrencyConflictException(new InvalidOperationException("xmin"));

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.ApproveAsync(AdminId, company.Id, TestContext.Current.CancellationToken));

        Assert.Equal("ERR-41", exception.Error.Code);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// A recusa passa a Empresa pendente a recusada com o motivo sem os espaços nas extremidades e regista
    /// <c>COMPANY_REJECTED</c> com o Administrador, numa única gravação (RF094, RF112, P12).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RejectRejectsThePendingCompanyWithTheTrimmedReasonAndLogsTheOperation()
    {
        Company company = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Pending, Created);

        await this.service.RejectAsync(
            AdminId,
            company.Id,
            new RejectCompanyRequest($"  {ValidReason}  "),
            TestContext.Current.CancellationToken);

        Assert.Equal(CompanyStatus.Rejected, company.Status);
        Assert.Equal(ValidReason, company.StatusReason);
        Assert.Equal([(AdminId, OperationType.CompanyRejected, company.Id)], this.logger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Um motivo com 10 ou 500 caracteres, depois de aparado, é aceite.
    /// </summary>
    /// <param name="length">Comprimento do motivo aparado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(10)]
    [InlineData(500)]
    public async Task RejectAcceptsTheLimitLengths(int length)
    {
        Company company = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Pending, Created);
        string reason = new('x', length);

        await this.service.RejectAsync(AdminId, company.Id, new RejectCompanyRequest($" {reason} "), TestContext.Current.CancellationToken);

        Assert.Equal(reason, company.StatusReason);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Um motivo em falta, vazio ou só com espaços dá <c>400</c> (ERR-10) no campo <c>reason</c>, sem gravar nem registar.
    /// </summary>
    /// <param name="reason">Motivo enviado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("     ")]
    public async Task RejectWithoutAReasonIsRequired(string? reason)
    {
        Company company = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Pending, Created);

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.RejectAsync(AdminId, company.Id, new RejectCompanyRequest(reason), TestContext.Current.CancellationToken));

        FieldErrorDto field = Assert.Single(exception.Fields);
        Assert.Equal("reason", field.Field);
        Assert.Equal("O campo é obrigatório.", field.Reason);
        Assert.Equal(CompanyStatus.Pending, company.Status);
        this.AssertNothingSaved();
    }

    /// <summary>
    /// Um motivo com 9 ou 501 caracteres, depois de aparado, dá <c>400</c> (ERR-10) no campo <c>reason</c>, sem gravar nem
    /// registar. Os espaços nas extremidades não contam.
    /// </summary>
    /// <param name="length">Comprimento do motivo aparado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(9)]
    [InlineData(501)]
    public async Task RejectWithAReasonOutOfRangeIsInvalid(int length)
    {
        Company company = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Pending, Created);
        var request = new RejectCompanyRequest($"  {new string('x', length)}  ");

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.RejectAsync(AdminId, company.Id, request, TestContext.Current.CancellationToken));

        FieldErrorDto field = Assert.Single(exception.Fields);
        Assert.Equal("reason", field.Field);
        Assert.Equal("O motivo tem de ter entre 10 e 500 caracteres.", field.Reason);
        Assert.Equal(CompanyStatus.Pending, company.Status);
        this.AssertNothingSaved();
    }

    /// <summary>
    /// Só uma Empresa pendente pode ser recusada: <c>409</c> (ERR-30) com o estado atual e nada é gravado.
    /// </summary>
    /// <param name="status">Estado atual da Empresa.</param>
    /// <param name="label">Designação do estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Approved, "aprovada")]
    [InlineData(CompanyStatus.Rejected, "recusada")]
    [InlineData(CompanyStatus.Suspended, "suspensa")]
    public async Task RejectingACompanyThatIsNotPendingIsAnInvalidState(CompanyStatus status, string label)
    {
        Company company = this.AddCompany("Lumen Software, Lda.", status, Created);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.RejectAsync(AdminId, company.Id, new RejectCompanyRequest(ValidReason), TestContext.Current.CancellationToken));

        Assert.Equal("ERR-30", exception.Error.Code);
        Assert.Equal($"A operação não é permitida no estado atual: {label}.", exception.Error.Message);
        Assert.Equal(status, company.Status);
        this.AssertNothingSaved();
    }

    /// <summary>
    /// Se a Empresa for alterada por outro pedido entre a leitura e a gravação, a recusa é recusada com <c>409</c>
    /// (ERR-41) e nada fica gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrentChangeWhileRejectingIsAConflict()
    {
        Company company = this.AddCompany("Lumen Software, Lda.", CompanyStatus.Pending, Created);
        this.unitOfWork.FailWith = new ConcurrencyConflictException(new InvalidOperationException("xmin"));

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.RejectAsync(AdminId, company.Id, new RejectCompanyRequest(ValidReason), TestContext.Current.CancellationToken));

        Assert.Equal("ERR-41", exception.Error.Code);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    private Company AddCompany(string name, CompanyStatus status, DateTimeOffset submittedAt)
    {
        var company = new Company(
            Guid.NewGuid(),
            new CompanyRegistration
            {
                CompanyName = name,
                TaxId = Interlocked.Increment(ref taxIdCounter).ToString(CultureInfo.InvariantCulture),
                Industry = Industry.Technology,
                Address = "Avenida da Liberdade, 100, 4710-000 Braga",
                LocationId = this.referenceData.Braga.Id,
                ContactEmail = "geral@exemplo.pt",
                ContactPhone = "253000111",
                ResponsibleName = "Rui Matos",
            },
            submittedAt);
        switch (status)
        {
            case CompanyStatus.Approved:
                company.Approve();
                break;
            case CompanyStatus.Rejected:
                company.Reject("Documentação em falta no pedido.");
                break;
            case CompanyStatus.Suspended:
                company.Approve();
                company.Suspend();
                break;
            default:
                break;
        }

        this.companies.All.Add(company);
        return company;
    }

    private void AssertNothingSaved()
    {
        Assert.Empty(this.logger.Entries);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    private sealed class FakeCompanies : ICompanyRepository
    {
        public List<Company> All { get; } = [];

        public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.All.SingleOrDefault(company => company.Id == id));
        }

        public Task<List<Company>> ListPendingAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.All.Where(company => company.Status == CompanyStatus.Pending).ToList());
        }
    }

    private sealed class FakeReferenceData : IReferenceDataRepository
    {
        public Location Braga { get; } = new("Braga", "Braga", 41.545450m, -8.426507m);

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<Location> { new("Porto", "Porto", 41.157944m, -8.629105m), this.Braga });
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
