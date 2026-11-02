// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Globalization;
using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IAccountAdminService"/> (modelo de classes do backend v02, secções 5.1 e 5.3).
/// </summary>
/// <remarks>
/// As transições de estado são as do <see cref="AppUser"/> (secção 3.1): o serviço verifica-as antes, para responder com
/// o ERR-30 e o estado atual. A mudança de estado da conta, o encerramento das conversas abertas (RF075) e a linha do
/// <c>operation_log</c> (RF112) são gravados numa única gravação, que é uma transação. A credencial já emitida deixa de
/// ser aceite no pedido seguinte da conta, porque o estado é lido da base em cada pedido (documentação da API v02,
/// secção 3.4).
/// </remarks>
public sealed class AccountAdminService : IAccountAdminService
{
    private static readonly StringComparer Alphabetical = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true);

    private readonly IUserRepository users;
    private readonly ICandidateRepository candidates;
    private readonly IReferenceDataRepository referenceData;
    private readonly IMatchRepository matches;
    private readonly IOperationLogger operationLogger;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountAdminService"/> class.
    /// </summary>
    /// <param name="users">Repositório das contas.</param>
    /// <param name="candidates">Repositório dos Candidatos, para a lista de Candidatos.</param>
    /// <param name="referenceData">Repositório das listas pré-definidas, para a designação da localidade.</param>
    /// <param name="matches">Repositório dos matches, para as conversas abertas da conta.</param>
    /// <param name="operationLogger">Registo das operações do RF112.</param>
    /// <param name="unitOfWork">Unidade de trabalho do pedido.</param>
    /// <param name="clock">Relógio do instante da mudança de estado.</param>
    public AccountAdminService(
        IUserRepository users,
        ICandidateRepository candidates,
        IReferenceDataRepository referenceData,
        IMatchRepository matches,
        IOperationLogger operationLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        this.users = users;
        this.candidates = candidates;
        this.referenceData = referenceData;
        this.matches = matches;
        this.operationLogger = operationLogger;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    /// <summary>
    /// Designação do estado da conta, para a mensagem do ERR-30 (documentação da API v02, secção 4.3).
    /// </summary>
    /// <param name="status">Estado da conta.</param>
    /// <returns>«ativa», «bloqueada» ou «suspensa».</returns>
    public static string AccountStatusLabel(AccountStatus status)
    {
        return status switch
        {
            AccountStatus.Active => "ativa",
            AccountStatus.Blocked => "bloqueada",
            AccountStatus.Suspended => "suspensa",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Estado de conta desconhecido."),
        };
    }

    /// <inheritdoc/>
    public async Task<List<AccountDto>> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        List<AppUser> accounts = await this.users.ListAsync(AccountFilter.All, cancellationToken);

        return accounts
            .OrderBy(user => user.Email, StringComparer.OrdinalIgnoreCase)
            .Select(user => new AccountDto(user.Id, user.Email, user.UserType, user.Status))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<List<CandidateAdminDto>> ListCandidatesAsync(CancellationToken cancellationToken = default)
    {
        List<Candidate> registered = await this.candidates.ListForAdminAsync(cancellationToken);
        Dictionary<Guid, string> locationNames = (await this.referenceData.ListLocationsAsync(cancellationToken))
            .ToDictionary(location => location.Id, ReferenceListService.DisplayName);

        return registered
            .Select(candidate => new CandidateAdminDto(
                candidate.Id,
                candidate.FullName,
                candidate.User.Email,
                locationNames.GetValueOrDefault(candidate.LocationId, string.Empty),
                candidate.User.CreatedAt,
                candidate.User.Status))
            .OrderBy(item => item.FullName, Alphabetical)
            .ThenBy(item => item.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task BlockAsync(Guid adminId, Guid userId, CancellationToken cancellationToken = default)
    {
        AppUser user = await this.users.GetByIdAsync(userId, cancellationToken)
            ?? throw new ElementNotFoundException();

        // A conta de Administrador nunca é bloqueada (RF086; AppUser.Block).
        if (user.UserType == UserType.Admin)
        {
            throw new OperationConflictException(ApiErrorResponse.InvalidState("conta de Administrador"));
        }

        EnsureActive(user);
        user.Block(this.clock.UtcNow);
        await this.CloseOpenConversationsAsync(user.Id, cancellationToken);
        await this.operationLogger.LogAsync(adminId, OperationType.AccountBlocked, user.Id, cancellationToken);
        await this.SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SuspendCandidateAsync(Guid adminId, Guid candidateId, CancellationToken cancellationToken = default)
    {
        AppUser? user = await this.users.GetByIdAsync(candidateId, cancellationToken);

        // Só a conta de Candidato pode ser suspensa; as restantes são tratadas como inexistentes nesta rota (6.10.4).
        if (user is null || user.UserType != UserType.Candidate)
        {
            throw new ElementNotFoundException();
        }

        EnsureActive(user);
        user.Suspend(this.clock.UtcNow);
        await this.CloseOpenConversationsAsync(user.Id, cancellationToken);
        await this.operationLogger.LogAsync(adminId, OperationType.AccountSuspended, user.Id, cancellationToken);
        await this.SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReactivateAsync(Guid adminId, Guid userId, CancellationToken cancellationToken = default)
    {
        AppUser user = await this.users.GetByIdAsync(userId, cancellationToken)
            ?? throw new ElementNotFoundException();

        if (user.Status == AccountStatus.Active)
        {
            throw new OperationConflictException(ApiErrorResponse.InvalidState(AccountStatusLabel(user.Status)));
        }

        // As conversas encerradas pelo bloqueio ou pela suspensão mantêm-se em consulta (RF075).
        user.Reactivate(this.clock.UtcNow);
        await this.operationLogger.LogAsync(adminId, OperationType.AccountReactivated, user.Id, cancellationToken);
        await this.SaveAsync(cancellationToken);
    }

    private static void EnsureActive(AppUser user)
    {
        if (user.Status != AccountStatus.Active)
        {
            throw new OperationConflictException(ApiErrorResponse.InvalidState(AccountStatusLabel(user.Status)));
        }
    }

    private async Task CloseOpenConversationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.clock.UtcNow;
        foreach (Match match in await this.matches.ListOpenConversationsByUserAsync(userId, cancellationToken))
        {
            match.CloseConversation(closedById: null, ConversationCloseReason.AccountBlockedOrSuspended, now);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Uma das conversas foi alterada por outro pedido (por exemplo, encerrada por uma das partes) entre a leitura e
            // a gravação: nada fica gravado e o Administrador repete a operação (modelo de dados, secção 7.3).
            throw new OperationConflictException(ApiErrorResponse.ConcurrencyConflict());
        }
    }
}
