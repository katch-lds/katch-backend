// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Linq.Expressions;
using System.Reflection;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Implementação de <see cref="IUserRepository"/> sobre o <see cref="KatchDbContext"/>. A comparação do correio usa
/// <c>lower(email)</c>, a mesma expressão do índice único <c>uq_app_user_email</c> (modelo de dados v02, secção 5.3).
/// </summary>
public sealed class UserRepository : IUserRepository
{
    // string.ToLower() sem argumentos é o método que os fornecedores do EF Core traduzem para lower() do SQL.
    private static readonly MethodInfo SqlLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    private readonly KatchDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class.
    /// </summary>
    /// <param name="context">Contexto da base de dados do pedido.</param>
    public UserRepository(KatchDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return this.context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return this.context.Users.SingleOrDefaultAsync(HasEmail(email), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return this.context.Users.AnyAsync(HasEmail(email), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await this.context.Users.AddAsync(user, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddRecruiterAsync(Recruiter recruiter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recruiter);

        await this.context.Recruiters.AddAsync(recruiter, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> CountActiveCandidatesAtAsync(DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        // Cada operação de conta tem um único estado anterior: o bloqueio e a suspensão partem de uma conta ativa e a
        // reativação de uma conta bloqueada ou suspensa (RF086, RF087, RF089, RF090).
        OperationType[] accountOperations =
            [OperationType.AccountBlocked, OperationType.AccountSuspended, OperationType.AccountReactivated];
        OperationType[] leavingActive = [OperationType.AccountBlocked, OperationType.AccountSuspended];

        return this.context.Users
            .Where(user => user.UserType == UserType.Candidate && user.CreatedAt < at)
            .Select(user => new
            {
                user.Status,
                NextOperation = this.context.OperationLogs
                    .Where(log => log.EntityId == user.Id
                        && accountOperations.Contains(log.Operation)
                        && log.OccurredAt >= at)
                    .OrderBy(log => log.OccurredAt)
                    .ThenBy(log => log.Id)
                    .Select(log => (OperationType?)log.Operation)
                    .FirstOrDefault(),
            })
            .CountAsync(
                user => user.NextOperation == null
                    ? user.Status == AccountStatus.Active
                    : leavingActive.Contains(user.NextOperation.Value),
                cancellationToken);
    }

    /// <summary>
    /// Condição <c>lower(email) = @email</c>, sem distinção de maiúsculas e servida pelo índice <c>uq_app_user_email</c>.
    /// </summary>
    /// <remarks>
    /// A expressão é montada à mão porque o Entity Framework Core só traduz para <c>lower()</c> do SQL o
    /// <see cref="string.ToLower()"/> sem argumentos (não traduz <see cref="string.ToLowerInvariant"/> nem
    /// <see cref="string.Equals(string, string, StringComparison)"/>); escrita como lambda, a comparação seria assinalada
    /// pelas regras CA1304, CA1311 e CA1862, que se aplicam a código executado em memória e não a uma consulta SQL.
    /// O valor fica numa propriedade de um objeto, para que o EF Core o envie como parâmetro e não como literal.
    /// </remarks>
    private static Expression<Func<AppUser, bool>> HasEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        ParameterExpression user = Expression.Parameter(typeof(AppUser), "user");
        Expression storedEmail = Expression.Call(Expression.Property(user, nameof(AppUser.Email)), SqlLower);
        Expression requestedEmail = Expression.Property(
            Expression.Constant(new EmailParameter(email.ToLowerInvariant())),
            nameof(EmailParameter.Value));

        return Expression.Lambda<Func<AppUser, bool>>(Expression.Equal(storedEmail, requestedEmail), user);
    }

    private sealed record EmailParameter(string Value);
}
