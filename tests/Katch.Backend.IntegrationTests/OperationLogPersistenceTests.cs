// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katch.Backend.IntegrationTests;

/// <summary>
/// Testes de persistência de <see cref="OperationLog"/> sobre SQLite in-memory: gravação e leitura, operação sem autor,
/// chave estrangeira para o autor e identificador gerado pela base de dados. O <c>CHECK</c> do autor e a identidade
/// <c>GENERATED ALWAYS</c> são específicos do PostgreSQL e estão verificados nos testes unitários do mapeamento e na
/// evidência da migration.
/// </summary>
public sealed class OperationLogPersistenceTests : IClassFixture<KatchWebApplicationFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

    private readonly KatchWebApplicationFactory factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationLogPersistenceTests"/> class.
    /// </summary>
    /// <param name="factory">Aplicação em memória partilhada pelos testes da classe.</param>
    public OperationLogPersistenceTests(KatchWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// A operação é gravada com o autor, a entidade e a data, o identificador é atribuído pela base de dados e a linha
    /// é lida de volta igual.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task OperationIsSavedAndReadBack()
    {
        AppUser user = await this.SeedUserAsync("gravar");
        Guid entityId = Guid.NewGuid();
        var log = OperationLog.Record(user.Id, OperationType.CompanyApproved, entityId, Now);
        Assert.Equal(0, log.Id);
        await this.SaveAsync(log);

        OperationLog saved = await this.ReadAsync(log.Id);

        Assert.True(log.Id > 0);
        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal(OperationType.CompanyApproved, saved.Operation);
        Assert.Equal(entityId, saved.EntityId);
        Assert.Equal(Now, saved.OccurredAt);
    }

    /// <summary>
    /// O encerramento automático de uma vaga é gravado sem autor.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task JobClosedWithoutAuthorIsSavedAndReadBack()
    {
        var log = OperationLog.Record(null, OperationType.JobClosed, Guid.NewGuid(), Now);
        await this.SaveAsync(log);

        OperationLog saved = await this.ReadAsync(log.Id);

        Assert.Null(saved.UserId);
        Assert.Equal(OperationType.JobClosed, saved.Operation);
    }

    /// <summary>
    /// O identificador gerado pela base de dados cresce a cada inserção.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task IdentifiersGrowWithEachInsertion()
    {
        var first = OperationLog.Record(null, OperationType.JobClosed, Guid.NewGuid(), Now);
        var second = OperationLog.Record(null, OperationType.JobClosed, Guid.NewGuid(), Now.AddMinutes(1));
        var third = OperationLog.Record(null, OperationType.JobClosed, Guid.NewGuid(), Now.AddMinutes(2));
        await this.SaveAsync(first);
        await this.SaveAsync(second);
        await this.SaveAsync(third);

        Assert.True(first.Id > 0);
        Assert.True(second.Id > first.Id);
        Assert.True(third.Id > second.Id);
    }

    /// <summary>
    /// Várias linhas gravadas de uma vez recebem identificadores distintos e crescentes.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LinesSavedTogetherGetDistinctGrowingIdentifiers()
    {
        OperationLog[] logs =
        [
            OperationLog.Record(null, OperationType.JobClosed, Guid.NewGuid(), Now),
            OperationLog.Record(null, OperationType.JobClosed, Guid.NewGuid(), Now),
            OperationLog.Record(null, OperationType.JobClosed, Guid.NewGuid(), Now),
        ];

        using (IServiceScope scope = this.factory.Services.CreateScope())
        {
            KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            context.OperationLogs.AddRange(logs);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(3, logs.Select(log => log.Id).Distinct().Count());
        Assert.Equal(logs.Select(log => log.Id).Order().ToArray(), logs.Select(log => log.Id).ToArray());
    }

    /// <summary>
    /// O histórico de uma entidade lê-se por ordem de data.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EntityHistoryIsReadInOrder()
    {
        AppUser user = await this.SeedUserAsync("historico");
        Guid entityId = Guid.NewGuid();
        await this.SaveAsync(OperationLog.Record(user.Id, OperationType.JobPublished, entityId, Now.AddHours(1)));
        await this.SaveAsync(OperationLog.Record(user.Id, OperationType.JobCreated, entityId, Now));
        await this.SaveAsync(OperationLog.Record(user.Id, OperationType.JobCreated, Guid.NewGuid(), Now));

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        List<OperationLog> history = await context.OperationLogs
            .AsNoTracking()
            .Where(item => item.EntityId == entityId)
            .ToListAsync(TestContext.Current.CancellationToken);

        // O SQLite não ordena DateTimeOffset; a ordenação é feita em memória.
        Assert.Equal(
            [OperationType.JobCreated, OperationType.JobPublished],
            history.OrderBy(item => item.OccurredAt).Select(item => item.Operation).ToArray());
    }

    /// <summary>
    /// Um autor inexistente é rejeitado pela chave estrangeira; a entidade registada não tem chave estrangeira.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task UnknownAuthorIsRejectedByTheForeignKey()
    {
        await Assert.ThrowsAsync<DbUpdateException>(
            () => this.SaveAsync(OperationLog.Record(Guid.NewGuid(), OperationType.JobCreated, Guid.NewGuid(), Now)));
    }

    private async Task<AppUser> SeedUserAsync(string name)
    {
        var user = new AppUser($"registo-{name}@katch.pt", "hash", UserType.Recruiter, Now, Now);
        var recruiter = new Recruiter(user);

        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        context.Recruiters.Add(recruiter);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    private async Task SaveAsync(OperationLog log)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        context.OperationLogs.Add(log);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<OperationLog> ReadAsync(long logId)
    {
        using IServiceScope scope = this.factory.Services.CreateScope();
        KatchDbContext context = scope.ServiceProvider.GetRequiredService<KatchDbContext>();
        return await context.OperationLogs
            .AsNoTracking()
            .SingleAsync(item => item.Id == logId, TestContext.Current.CancellationToken);
    }
}
