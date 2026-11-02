// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Katch.Backend.UnitTests.Repositories;

/// <summary>
/// Testes de <see cref="UnitOfWork"/>: conversão da falha de <c>xmin</c> em <see cref="ConcurrencyConflictException"/> e
/// descarte do contexto, sem base de dados (a gravação é interrompida por um intercetor).
/// </summary>
public sealed class UnitOfWorkTests
{
    /// <summary>
    /// A falha de concorrência do Entity Framework Core é convertida em <see cref="ConcurrencyConflictException"/>, com a
    /// exceção original como causa.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConvertsDbUpdateConcurrencyExceptionToConcurrencyConflict()
    {
        var original = new DbUpdateConcurrencyException("xmin");
        using KatchDbContext context = CreateContext(new ThrowingInterceptor(original));
        context.Locations.Add(new Location("Braga", "Braga", 41.545450m, -8.426507m));
        var unitOfWork = new UnitOfWork(context);

        ConcurrencyConflictException exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Same(original, exception.InnerException);
    }

    /// <summary>
    /// A violação de um índice único (SQLSTATE 23505) é convertida em <see cref="UniqueConstraintViolationException"/>,
    /// com o nome do índice.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConvertsUniqueViolationToUniqueConstraintViolation()
    {
        var original = new DbUpdateException("unique", CreatePostgresException("23505", "uq_candidate_custom_skill"));
        using KatchDbContext context = CreateContext(new ThrowingInterceptor(original));
        context.Locations.Add(new Location("Braga", "Braga", 41.545450m, -8.426507m));
        var unitOfWork = new UnitOfWork(context);

        UniqueConstraintViolationException exception = await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal("uq_candidate_custom_skill", exception.ConstraintName);
        Assert.Same(original, exception.InnerException);
    }

    /// <summary>
    /// A violação de uma restrição <c>CHECK</c> (SQLSTATE 23514) é convertida em
    /// <see cref="CheckConstraintViolationException"/>, com o nome da restrição.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ConvertsCheckViolationToCheckConstraintViolation()
    {
        var original = new DbUpdateException("check", CreatePostgresException("23514", "ck_candidate_custom_skill_label"));
        using KatchDbContext context = CreateContext(new ThrowingInterceptor(original));
        context.Locations.Add(new Location("Braga", "Braga", 41.545450m, -8.426507m));
        var unitOfWork = new UnitOfWork(context);

        CheckConstraintViolationException exception = await Assert.ThrowsAsync<CheckConstraintViolationException>(
            () => unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal("ck_candidate_custom_skill_label", exception.ConstraintName);
        Assert.Same(original, exception.InnerException);
    }

    /// <summary>
    /// <see cref="UnitOfWork.DiscardChanges"/> deixa o contexto sem nenhuma entidade acompanhada.
    /// </summary>
    [Fact]
    public void DiscardChangesClearsTheChangeTracker()
    {
        using KatchDbContext context = CreateContext(null);
        context.Locations.Add(new Location("Braga", "Braga", 41.545450m, -8.426507m));
        var unitOfWork = new UnitOfWork(context);
        Assert.NotEmpty(context.ChangeTracker.Entries());

        unitOfWork.DiscardChanges();

        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static PostgresException CreatePostgresException(string sqlState, string constraintName)
    {
        return new PostgresException(
            "violação",
            "ERROR",
            "ERROR",
            sqlState,
            constraintName: constraintName);
    }

    private static KatchDbContext CreateContext(ISaveChangesInterceptor? interceptor)
    {
        var builder = new DbContextOptionsBuilder<KatchDbContext>();
        builder.UseKatchPostgres("Host=localhost;Database=katch");
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new KatchDbContext(builder.Options);
    }

    private sealed class ThrowingInterceptor : SaveChangesInterceptor
    {
        private readonly Exception exception;

        public ThrowingInterceptor(Exception exception)
        {
            this.exception = exception;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            throw this.exception;
        }
    }
}
