// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Katch.Backend.UnitTests.Infrastructure.Persistence;

/// <summary>
/// Testes das operações da migration <see cref="AddCompany"/>: a tabela <c>company</c> com as restrições <c>CHECK</c>, as
/// chaves estrangeiras e os índices do modelo de dados v02 (secções 5.2, 5.3 e 7.1), e a reversão no <c>Down</c>, sem
/// precisar de uma base de dados.
/// </summary>
public sealed class AddCompanyMigrationTests
{
    /// <summary>
    /// O <c>Up</c> cria a tabela <c>company</c> com as dezassete colunas, as dez restrições <c>CHECK</c> e as chaves
    /// estrangeiras para <c>location</c> e <c>recruiter</c>.
    /// </summary>
    [Fact]
    public void UpCreatesTheCompanyTableWithChecksAndForeignKeys()
    {
        var migration = new AddCompany();

        CreateTableOperation table = Assert.IsType<CreateTableOperation>(migration.UpOperations[0]);

        Assert.Equal("company", table.Name);
        Assert.Equal(17, table.Columns.Count);
        Assert.Equal(
            [
                "ck_company_address",
                "ck_company_company_name",
                "ck_company_contact_email",
                "ck_company_contact_phone",
                "ck_company_photo_paths",
                "ck_company_reason",
                "ck_company_reason_length",
                "ck_company_responsible_name",
                "ck_company_tax_id",
                "ck_company_website",
            ],
            table.CheckConstraints.Select(check => check.Name).ToArray());
        Assert.Equal(
            ["location", "recruiter"],
            table.ForeignKeys.Select(foreignKey => foreignKey.PrincipalTable).ToArray());
    }

    /// <summary>
    /// O <c>Up</c> cria os três índices da secção 5.3: o índice por estado e os índices únicos do Recrutador e do NIF.
    /// </summary>
    [Fact]
    public void UpCreatesTheStatusIndexAndTheUniqueIndexes()
    {
        var migration = new AddCompany();

        CreateIndexOperation[] indexes = [.. migration.UpOperations.OfType<CreateIndexOperation>()];

        Assert.Equal(4, migration.UpOperations.Count);
        Assert.Equal(
            ["ix_company_status", "uq_company_recruiter", "uq_company_tax_id"],
            indexes.Select(index => index.Name).ToArray());
        Assert.Equal([false, true, true], indexes.Select(index => index.IsUnique).ToArray());
        Assert.Equal(["status", "recruiter_id", "tax_id"], indexes.Select(index => index.Columns.Single()).ToArray());
    }

    /// <summary>
    /// O <c>Down</c> apaga a tabela <c>company</c>, com as restrições e os índices.
    /// </summary>
    [Fact]
    public void DownDropsTheCompanyTable()
    {
        var migration = new AddCompany();

        DropTableOperation table = Assert.IsType<DropTableOperation>(Assert.Single(migration.DownOperations));

        Assert.Equal("company", table.Name);
    }
}
