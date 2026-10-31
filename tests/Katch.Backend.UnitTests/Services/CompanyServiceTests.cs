// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Repositories;
using Katch.Backend.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes do registo da Empresa, da nova submissão e da consulta do estado em <see cref="CompanyService"/>
/// (documentação da API v02, secções 6.4.1 a 6.4.3; RF041 a RF045, RF112, P04, P15), com repositórios em memória.
/// </summary>
public sealed class CompanyServiceTests
{
    private const string RejectionReason = "A morada indicada não corresponde à certidão permanente.";

    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Braga = Guid.Parse("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c07");
    private static readonly Guid RecruiterId = Guid.Parse("8b1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e02");

    private readonly FakeCompanies companies = new();
    private readonly FakeOperationLogger logger = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeClock clock = new() { UtcNow = Now };
    private readonly CompanyService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyServiceTests"/> class.
    /// </summary>
    public CompanyServiceTests()
    {
        this.service = new CompanyService(
            this.companies,
            new FakeReferenceData(Braga),
            this.logger,
            this.unitOfWork,
            this.clock);
    }

    /// <summary>
    /// Gets the cases of one invalid field at a time, with the reason of the catalogue (RF042, P04, P15): field, invalid
    /// value and expected reason.
    /// </summary>
    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "companyName", " ", "O campo é obrigatório." },
        { "companyName", new string('a', 201), "O texto não pode exceder 200 caracteres." },
        { "taxId", string.Empty, "O campo é obrigatório." },
        { "taxId", "50912345", "O número de identificação fiscal tem de ter 9 algarismos." },
        { "taxId", "5091234567", "O número de identificação fiscal tem de ter 9 algarismos." },
        { "taxId", "50912345a", "O número de identificação fiscal tem de ter 9 algarismos." },
        { "address", string.Empty, "O campo é obrigatório." },
        { "address", new string('a', 256), "O texto não pode exceder 255 caracteres." },
        { "contactEmail", string.Empty, "O campo é obrigatório." },
        { "contactEmail", "geral.lumen-exemplo.pt", "O endereço de correio eletrónico tem de ter o formato local@domínio." },
        { "contactEmail", "geral@@lumen-exemplo.pt", "O endereço de correio eletrónico tem de ter o formato local@domínio." },
        { "contactEmail", new string('a', 251) + "@x.pt", "O endereço de correio eletrónico tem no máximo 255 caracteres." },
        { "contactPhone", string.Empty, "O campo é obrigatório." },
        { "contactPhone", "25300011", "O contacto telefónico tem de ter 9 algarismos." },
        { "contactPhone", "253 000 111", "O contacto telefónico tem de ter 9 algarismos." },
        { "responsibleName", string.Empty, "O campo é obrigatório." },
        { "responsibleName", new string('a', 151), "O texto não pode exceder 150 caracteres." },
    };

    /// <summary>
    /// Com dados válidos, a Empresa fica pendente, com a data de submissão, e a submissão é registada no
    /// <c>operation_log</c> com o Recrutador como autor, na mesma gravação (RF043, RF112).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ValidRegistrationCreatesAPendingCompanyAndLogsTheSubmission()
    {
        CompanyStatusDto status = await this.service.RegisterAsync(RecruiterId, Valid(), TestContext.Current.CancellationToken);

        Company company = Assert.Single(this.companies.Added);
        Assert.Equal(RecruiterId, company.RecruiterId);
        Assert.Equal(CompanyStatus.Pending, company.Status);
        Assert.Null(company.StatusReason);
        Assert.Equal(Now, company.SubmittedAt);
        Assert.Equal("Lumen Software, Lda.", company.CompanyName);
        Assert.Equal("509123456", company.TaxId);
        Assert.Equal(Industry.Technology, company.Industry);
        Assert.Equal(Braga, company.LocationId);
        Assert.Equal(new CompanyStatusDto(company.Id, CompanyStatus.Pending, null, Now), status);
        Assert.Equal([(RecruiterId, OperationType.CompanySubmitted, company.Id)], this.logger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Os espaços no início e no fim dos textos são retirados antes da validação e da gravação (API v02, secção 2.3).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TextsAreTrimmedBeforeValidationAndStorage()
    {
        CompanyRegistrationRequest request = Valid() with
        {
            CompanyName = "  Lumen Software, Lda. ",
            TaxId = " 509123456 ",
            ContactEmail = " geral@lumen-exemplo.pt ",
            ContactPhone = " 253000111 ",
            ResponsibleName = " Rui Matos ",
        };

        await this.service.RegisterAsync(RecruiterId, request, TestContext.Current.CancellationToken);

        Company company = Assert.Single(this.companies.Added);
        Assert.Equal("Lumen Software, Lda.", company.CompanyName);
        Assert.Equal("509123456", company.TaxId);
        Assert.Equal("geral@lumen-exemplo.pt", company.ContactEmail);
        Assert.Equal("253000111", company.ContactPhone);
        Assert.Equal("Rui Matos", company.ResponsibleName);
    }

    /// <summary>
    /// Com todos os campos inválidos, o ERR-10 indica cada campo no mesmo pedido e nada é gravado nem registado (RF042).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task AllInvalidFieldsAreReportedInTheSameResponse()
    {
        var request = new CompanyRegistrationRequest(
            " ",
            "123",
            (Industry)99,
            " ",
            Guid.NewGuid(),
            "geral",
            "253",
            " ");

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.RegisterAsync(RecruiterId, request, TestContext.Current.CancellationToken));

        Assert.Equal(
            [
                ("companyName", "O campo é obrigatório."),
                ("taxId", "O número de identificação fiscal tem de ter 9 algarismos."),
                ("industry", "O valor não pertence à lista pré-definida."),
                ("address", "O campo é obrigatório."),
                ("locationId", "O valor não pertence à lista pré-definida."),
                ("contactEmail", "O endereço de correio eletrónico tem de ter o formato local@domínio."),
                ("contactPhone", "O contacto telefónico tem de ter 9 algarismos."),
                ("responsibleName", "O campo é obrigatório."),
            ],
            exception.Fields.Select(field => (field.Field, field.Reason)));
        Assert.Empty(this.companies.Added);
        Assert.Empty(this.logger.Entries);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Sem nenhum campo, todos são indicados como obrigatórios.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EmptyRequestReportsEveryFieldAsRequired()
    {
        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.RegisterAsync(
                RecruiterId,
                CompanyRegistrationRequest.Empty,
                TestContext.Current.CancellationToken));

        Assert.Equal(
            ["companyName", "taxId", "industry", "address", "locationId", "contactEmail", "contactPhone", "responsibleName"],
            exception.Fields.Select(field => field.Field));
        Assert.All(exception.Fields, field => Assert.Equal("O campo é obrigatório.", field.Reason));
    }

    /// <summary>
    /// Cada campo inválido é recusado com o seu motivo, sem afetar os restantes (RF042, P04).
    /// </summary>
    /// <param name="field">Campo alterado.</param>
    /// <param name="value">Valor inválido.</param>
    /// <param name="reason">Motivo esperado.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task InvalidFieldIsRejectedWithItsReason(string field, string value, string reason)
    {
        CompanyRegistrationRequest request = field switch
        {
            "companyName" => Valid() with { CompanyName = value },
            "taxId" => Valid() with { TaxId = value },
            "address" => Valid() with { Address = value },
            "contactEmail" => Valid() with { ContactEmail = value },
            "contactPhone" => Valid() with { ContactPhone = value },
            _ => Valid() with { ResponsibleName = value },
        };

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.RegisterAsync(RecruiterId, request, TestContext.Current.CancellationToken));

        FieldErrorDto error = Assert.Single(exception.Fields);
        Assert.Equal(field, error.Field);
        Assert.Equal(reason, error.Reason);
    }

    /// <summary>
    /// O NIF já registado noutra Empresa é recusado com o ERR-10 do campo <c>taxId</c> (RF042).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task TaxIdOfAnotherCompanyIsRejected()
    {
        this.companies.Existing.Add(NewCompany(Guid.NewGuid(), "509123456"));

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.RegisterAsync(RecruiterId, Valid(), TestContext.Current.CancellationToken));

        FieldErrorDto error = Assert.Single(exception.Fields);
        Assert.Equal(("taxId", "O número de identificação fiscal já está registado."), (error.Field, error.Reason));
        Assert.Empty(this.companies.Added);
    }

    /// <summary>
    /// Um segundo registo do mesmo Recrutador devolve o ERR-40, mesmo com dados inválidos, e nada é gravado.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task SecondRegistrationOfTheSameRecruiterReturnsErr40()
    {
        this.companies.Existing.Add(NewCompany(RecruiterId, "509123456"));

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.RegisterAsync(
                RecruiterId,
                CompanyRegistrationRequest.Empty,
                TestContext.Current.CancellationToken));

        Assert.Equal("ERR-40", exception.Error.Code);
        Assert.Equal("O Recrutador já registou uma Empresa.", exception.Error.Message);
        Assert.Empty(this.companies.Added);
        Assert.Empty(this.logger.Entries);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Se outro pedido gravou o mesmo NIF entre a verificação e a gravação, o índice único devolve o mesmo ERR-10.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrentTaxIdIsReportedAsTaxIdTaken()
    {
        this.unitOfWork.FailWith = new UniqueConstraintViolationException("uq_company_tax_id", new InvalidOperationException());

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.RegisterAsync(RecruiterId, Valid(), TestContext.Current.CancellationToken));

        FieldErrorDto error = Assert.Single(exception.Fields);
        Assert.Equal(("taxId", "O número de identificação fiscal já está registado."), (error.Field, error.Reason));
    }

    /// <summary>
    /// Se outro pedido registou uma Empresa do mesmo Recrutador entre a verificação e a gravação, a resposta é o ERR-40.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConcurrentSecondCompanyIsReportedAsErr40()
    {
        this.unitOfWork.FailWith = new UniqueConstraintViolationException(
            "uq_company_recruiter",
            new InvalidOperationException());

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.RegisterAsync(RecruiterId, Valid(), TestContext.Current.CancellationToken));

        Assert.Equal("ERR-40", exception.Error.Code);
    }

    /// <summary>
    /// A nova submissão de um pedido recusado passa a Empresa a pendente, limpa o motivo, atualiza a data de submissão
    /// e os dados, e regista <c>COMPANY_SUBMITTED</c> (RF045, RF112). O NIF pode ser mantido.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ResubmissionOfARejectedCompanyReturnsToPendingAndClearsTheReason()
    {
        Company company = NewCompany(RecruiterId, "509123456");
        company.Reject(RejectionReason);
        this.companies.Existing.Add(company);
        DateTimeOffset later = Now.AddDays(1);
        this.clock.UtcNow = later;

        CompanyStatusDto status = await this.service.ResubmitAsync(
            RecruiterId,
            Valid() with { Address = "Avenida da Liberdade, 102, 4710-000 Braga" },
            TestContext.Current.CancellationToken);

        Assert.Equal(new CompanyStatusDto(company.Id, CompanyStatus.Pending, null, later), status);
        Assert.Equal(CompanyStatus.Pending, company.Status);
        Assert.Null(company.StatusReason);
        Assert.Equal(later, company.SubmittedAt);
        Assert.Equal("Avenida da Liberdade, 102, 4710-000 Braga", company.Address);
        Assert.Equal([(RecruiterId, OperationType.CompanySubmitted, company.Id)], this.logger.Entries);
        Assert.Equal(1, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Na nova submissão, o NIF pode ser corrigido, mas não para o de outra Empresa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ResubmissionWithTheTaxIdOfAnotherCompanyIsRejected()
    {
        Company company = NewCompany(RecruiterId, "509123456");
        company.Reject(RejectionReason);
        this.companies.Existing.Add(company);
        this.companies.Existing.Add(NewCompany(Guid.NewGuid(), "509999999"));

        ValidationFailedException exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => this.service.ResubmitAsync(
                RecruiterId,
                Valid() with { TaxId = "509999999" },
                TestContext.Current.CancellationToken));

        Assert.Equal("taxId", Assert.Single(exception.Fields).Field);
        Assert.Equal(CompanyStatus.Rejected, company.Status);
        Assert.Empty(this.logger.Entries);
    }

    /// <summary>
    /// A nova submissão só é aceite no estado recusado: nos restantes devolve o ERR-30 com o estado atual e nada muda.
    /// </summary>
    /// <param name="status">Estado da Empresa.</param>
    /// <param name="label">Estado na mensagem.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending, "pendente")]
    [InlineData(CompanyStatus.Approved, "aprovada")]
    [InlineData(CompanyStatus.Suspended, "suspensa")]
    public async Task ResubmissionOutsideTheRejectedStateReturnsErr30(CompanyStatus status, string label)
    {
        Company company = NewCompany(RecruiterId, "509123456", status);
        this.companies.Existing.Add(company);

        OperationConflictException exception = await Assert.ThrowsAsync<OperationConflictException>(
            () => this.service.ResubmitAsync(RecruiterId, Valid(), TestContext.Current.CancellationToken));

        Assert.Equal("ERR-30", exception.Error.Code);
        Assert.Equal($"A operação não é permitida no estado atual: {label}.", exception.Error.Message);
        Assert.Equal(status, company.Status);
        Assert.Equal(Now, company.SubmittedAt);
        Assert.Empty(this.logger.Entries);
        Assert.Equal(0, this.unitOfWork.Saves);
    }

    /// <summary>
    /// Sem Empresa registada, a nova submissão devolve o ERR-20.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ResubmissionWithoutACompanyReturnsErr20()
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.ResubmitAsync(RecruiterId, Valid(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A consulta do estado devolve o motivo da recusa no estado recusado (RF044).
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task StatusOfARejectedCompanyIncludesTheReason()
    {
        Company company = NewCompany(RecruiterId, "509123456");
        company.Reject(RejectionReason);
        this.companies.Existing.Add(company);

        CompanyStatusDto status = await this.service.GetStatusAsync(RecruiterId, TestContext.Current.CancellationToken);

        Assert.Equal(new CompanyStatusDto(company.Id, CompanyStatus.Rejected, RejectionReason, Now), status);
    }

    /// <summary>
    /// Nos restantes estados, a consulta do estado não tem motivo.
    /// </summary>
    /// <param name="status">Estado da Empresa.</param>
    /// <returns>Tarefa do teste.</returns>
    [Theory]
    [InlineData(CompanyStatus.Pending)]
    [InlineData(CompanyStatus.Approved)]
    [InlineData(CompanyStatus.Suspended)]
    public async Task StatusOutsideTheRejectedStateHasNoReason(CompanyStatus status)
    {
        Company company = NewCompany(RecruiterId, "509123456", status);
        this.companies.Existing.Add(company);

        CompanyStatusDto result = await this.service.GetStatusAsync(RecruiterId, TestContext.Current.CancellationToken);

        Assert.Equal(new CompanyStatusDto(company.Id, status, null, Now), result);
    }

    /// <summary>
    /// Sem Empresa registada, a consulta do estado devolve o ERR-20.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task StatusWithoutACompanyReturnsErr20()
    {
        await Assert.ThrowsAsync<ElementNotFoundException>(
            () => this.service.GetStatusAsync(RecruiterId, TestContext.Current.CancellationToken));
    }

    private static CompanyRegistrationRequest Valid()
    {
        return new CompanyRegistrationRequest(
            "Lumen Software, Lda.",
            "509123456",
            Industry.Technology,
            "Avenida da Liberdade, 100, 4710-000 Braga",
            Braga,
            "geral@lumen-exemplo.pt",
            "253000111",
            "Rui Matos");
    }

    private static Company NewCompany(Guid recruiterId, string taxId, CompanyStatus status = CompanyStatus.Pending)
    {
        var company = new Company(
            recruiterId,
            new CompanyRegistration
            {
                CompanyName = "Empresa de teste",
                TaxId = taxId,
                Industry = Industry.Services,
                Address = "Rua de teste, 1",
                LocationId = Braga,
                ContactEmail = "teste@exemplo.pt",
                ContactPhone = "253000000",
                ResponsibleName = "Responsável",
            },
            Now);

        if (status is CompanyStatus.Approved or CompanyStatus.Suspended)
        {
            company.Approve();
        }

        if (status == CompanyStatus.Suspended)
        {
            company.Suspend();
        }

        if (status == CompanyStatus.Rejected)
        {
            company.Reject(RejectionReason);
        }

        return company;
    }

    private sealed class FakeCompanies : ICompanyRepository
    {
        public List<Company> Existing { get; } = [];

        public List<Company> Added { get; } = [];

        public Task<Company?> GetByRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Existing.Concat(this.Added).SingleOrDefault(c => c.RecruiterId == recruiterId));
        }

        public Task<bool> TaxIdExistsAsync(string taxId, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Existing.Any(c => c.TaxId == taxId && c.Id != exceptId));
        }

        public Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            this.Added.Add(company);
            return Task.CompletedTask;
        }

        public Task<int> CountByStatusAtAsync(CompanyStatus status, DateTimeOffset at, CancellationToken cancellationToken = default)
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

    private sealed class FakeReferenceData : IReferenceDataRepository
    {
        private readonly Guid locationId;

        public FakeReferenceData(Guid locationId)
        {
            this.locationId = locationId;
        }

        public Task<List<Location>> ListLocationsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<Location>());
        }

        public Task<bool> LocationExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == this.locationId);
        }

        public Task<List<Benefit>> ListBenefitsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Skill?> GetSkillAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Benefit?> GetBenefitAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> SkillNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> BenefitNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddSkillAsync(Skill skill, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddBenefitAsync(Benefit benefit, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
            return Task.FromResult(2);
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

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
