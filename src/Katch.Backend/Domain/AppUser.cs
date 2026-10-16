// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Domain;

/// <summary>
/// Conta de utilizador, comum aos três tipos de conta (tabela <c>app_user</c>). O Candidato, o Recrutador e o
/// Administrador especializam-na com a mesma chave primária.
/// </summary>
/// <remarks>
/// A entidade aplica apenas as regras de estado que dependem da própria linha (modelo de dados v02, secção 7.1):
/// <c>SUSPENDED</c> só em contas de Candidato, Administrador sempre ativo e aceitação das condições de utilização
/// obrigatória fora do Administrador. A autoria e a data das mudanças de estado ficam no <c>OperationLog</c>
/// (DM-18), e <see cref="CreatedAt"/> e <see cref="UpdatedAt"/> só existem nesta entidade (DM-22).
/// </remarks>
public sealed class AppUser
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppUser"/> class com o estado <see cref="AccountStatus.Active"/>.
    /// </summary>
    /// <param name="email">Endereço de correio eletrónico, no formato local@domínio.</param>
    /// <param name="passwordHash">Resumo criptográfico da palavra-passe; a palavra-passe nunca é guardada.</param>
    /// <param name="userType">Tipo de conta.</param>
    /// <param name="termsAcceptedAt">Instante da aceitação das condições; obrigatório fora do Administrador.</param>
    /// <param name="now">Instante do registo, em UTC.</param>
    /// <exception cref="ArgumentException">
    /// Se o correio eletrónico não tiver o formato local@domínio, se o resumo estiver vazio ou se faltar a aceitação
    /// das condições numa conta que não seja de Administrador.
    /// </exception>
    public AppUser(string email, string passwordHash, UserType userType, DateTimeOffset? termsAcceptedAt, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ValidateEmail(email);

        if (userType != UserType.Admin && termsAcceptedAt is null)
        {
            throw new ArgumentException(
                "A aceitação das condições de utilização é obrigatória nas contas de Candidato e de Recrutador.",
                nameof(termsAcceptedAt));
        }

        this.Id = Guid.NewGuid();
        this.Email = email;
        this.PasswordHash = passwordHash;
        this.UserType = userType;
        this.Status = AccountStatus.Active;
        this.TermsAcceptedAt = termsAcceptedAt;
        this.CreatedAt = now;
        this.UpdatedAt = now;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUser"/> class. Usado pelo Entity Framework Core.
    /// </summary>
    private AppUser()
    {
        this.Email = string.Empty;
        this.PasswordHash = string.Empty;
    }

    /// <summary>
    /// Gets the identifier of the account (<c>id</c>), shared with the specialization of the account.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the e-mail address (<c>email</c>), unique in the system without distinction of case.
    /// </summary>
    public string Email { get; private set; }

    /// <summary>
    /// Gets the irreversible hash of the password (<c>password_hash</c>).
    /// </summary>
    public string PasswordHash { get; private set; }

    /// <summary>
    /// Gets the type of account (<c>user_type</c>), which determines the access point and the authorized operations.
    /// </summary>
    public UserType UserType { get; private set; }

    /// <summary>
    /// Gets the state of the account (<c>status</c>).
    /// </summary>
    public AccountStatus Status { get; private set; }

    /// <summary>
    /// Gets the instant at which the terms of use were accepted (<c>terms_accepted_at</c>).
    /// </summary>
    public DateTimeOffset? TermsAcceptedAt { get; private set; }

    /// <summary>
    /// Gets the date of registration (<c>created_at</c>).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the instant of the last change (<c>updated_at</c>).
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Indica se a conta pode iniciar sessão: só as contas ativas o podem fazer (RF086, RF089).
    /// </summary>
    /// <returns><see langword="true"/> se o estado for <see cref="AccountStatus.Active"/>.</returns>
    public bool CanSignIn()
    {
        return this.Status == AccountStatus.Active;
    }

    /// <summary>
    /// Bloqueia a conta (RF086). Aplica-se a contas de Candidato e de Recrutador ativas.
    /// </summary>
    /// <param name="now">Instante da alteração, em UTC.</param>
    /// <exception cref="InvalidOperationException">
    /// Se a conta for de Administrador ou se não estiver ativa.
    /// </exception>
    public void Block(DateTimeOffset now)
    {
        if (this.UserType == UserType.Admin)
        {
            throw new InvalidOperationException("A conta de Administrador não pode ser bloqueada.");
        }

        this.ChangeStatusFromActive(AccountStatus.Blocked, now);
    }

    /// <summary>
    /// Suspende a conta (RF089). Aplica-se só a contas de Candidato ativas.
    /// </summary>
    /// <param name="now">Instante da alteração, em UTC.</param>
    /// <exception cref="InvalidOperationException">
    /// Se a conta não for de Candidato ou se não estiver ativa.
    /// </exception>
    public void Suspend(DateTimeOffset now)
    {
        if (this.UserType != UserType.Candidate)
        {
            throw new InvalidOperationException("Só a conta de Candidato pode ser suspensa.");
        }

        this.ChangeStatusFromActive(AccountStatus.Suspended, now);
    }

    /// <summary>
    /// Reativa uma conta bloqueada (RF087) ou suspensa (RF090), que passa a ativa.
    /// </summary>
    /// <param name="now">Instante da alteração, em UTC.</param>
    /// <exception cref="InvalidOperationException">Se a conta já estiver ativa.</exception>
    public void Reactivate(DateTimeOffset now)
    {
        if (this.Status == AccountStatus.Active)
        {
            throw new InvalidOperationException("A conta já está ativa.");
        }

        this.Status = AccountStatus.Active;
        this.UpdatedAt = now;
    }

    /// <summary>
    /// Substitui o resumo da palavra-passe (RF040). A regra P05 é validada antes do cálculo do resumo.
    /// </summary>
    /// <param name="hash">Novo resumo criptográfico da palavra-passe.</param>
    /// <param name="now">Instante da alteração, em UTC.</param>
    /// <exception cref="ArgumentException">Se o resumo estiver vazio.</exception>
    public void SetPasswordHash(string hash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        this.PasswordHash = hash;
        this.UpdatedAt = now;
    }

    private static void ValidateEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        // Mesma regra do CHECK ck_app_user_email: exatamente um '@', com texto sem espaços de cada lado.
        int at = email.IndexOf('@', StringComparison.Ordinal);
        bool valid = at > 0
            && at == email.LastIndexOf('@')
            && at < email.Length - 1
            && !email.Any(char.IsWhiteSpace);

        if (!valid)
        {
            throw new ArgumentException("O correio eletrónico deve ter o formato local@dominio.", nameof(email));
        }
    }

    private void ChangeStatusFromActive(AccountStatus target, DateTimeOffset now)
    {
        if (this.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("Só uma conta ativa pode mudar para esse estado.");
        }

        this.Status = target;
        this.UpdatedAt = now;
    }
}
