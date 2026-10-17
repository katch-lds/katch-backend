// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.RegularExpressions;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Domain;

/// <summary>
/// Conta de Candidato e perfil profissional (tabela <c>candidate</c>): especialização de <see cref="AppUser"/> com a
/// chave primária partilhada. Guarda as preferências de procura, a quota de interesses e as hiperligações profissionais.
/// </summary>
/// <remarks>
/// Os formatos dos dados (telefone, distância, pretensão salarial, hiperligações) são validados pelos DTOs (P04, P06,
/// P10) e garantidos também pelos <c>CHECK</c> da tabela. As operações da quota e das hiperligações aplicam as regras que
/// dependem só desta linha. A concorrência é controlada por <see cref="Version"/>, que corresponde à coluna de sistema
/// <c>xmin</c> do PostgreSQL (modelo de dados v02, secção 7.3).
/// </remarks>
public sealed partial class Candidate
{
    /// <summary>
    /// Número de interesses da quota (P11), que é reposta na totalidade quando termina o período de bloqueio.
    /// </summary>
    public const short MaxInterests = 10;

    /// <summary>
    /// Número máximo de hiperligações profissionais por Candidato (P10).
    /// </summary>
    public const int MaxLinks = 3;

    /// <summary>
    /// Período de bloqueio depois de esgotada a quota, contado do interesse que a esgotou (P11).
    /// </summary>
    public static readonly TimeSpan BlockPeriod = TimeSpan.FromHours(24);

    private readonly List<CandidateLink> links = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Candidate"/> class com a quota de interesses completa.
    /// </summary>
    /// <param name="user">Conta de utilizador do tipo <see cref="UserType.Candidate"/>.</param>
    /// <param name="fullName">Nome do Candidato, não vazio e com no máximo 150 caracteres.</param>
    /// <param name="phoneNumber">Contacto telefónico com exatamente 9 algarismos (P04).</param>
    /// <param name="locationId">Identificador da localidade do Candidato.</param>
    /// <exception cref="ArgumentException">
    /// Se a conta não for de Candidato, se o nome estiver vazio ou for demasiado longo ou se o telefone não tiver 9 algarismos.
    /// </exception>
    public Candidate(AppUser user, string fullName, string phoneNumber, Guid locationId)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fullName.Length, 150);
        ArgumentNullException.ThrowIfNull(phoneNumber);

        if (user.UserType != UserType.Candidate)
        {
            throw new ArgumentException("A conta tem de ser do tipo Candidato.", nameof(user));
        }

        if (!PhoneNumberPattern().IsMatch(phoneNumber))
        {
            throw new ArgumentException("O telefone deve ter exatamente 9 algarismos.", nameof(phoneNumber));
        }

        this.Id = user.Id;
        this.User = user;
        this.FullName = fullName;
        this.PhoneNumber = phoneNumber;
        this.LocationId = locationId;
        this.SwipeRightsRemaining = MaxInterests;
    }

    private Candidate()
    {
        this.User = null!;
        this.FullName = string.Empty;
        this.PhoneNumber = string.Empty;
    }

    /// <summary>
    /// Gets the identifier of the candidate (<c>id</c>), primary key and foreign key to <c>app_user</c>.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the account of the user that this candidate specializes.
    /// </summary>
    public AppUser User { get; private set; }

    /// <summary>
    /// Gets or sets the name of the candidate (<c>full_name</c>).
    /// </summary>
    public string FullName { get; set; }

    /// <summary>
    /// Gets or sets the phone number (<c>phone_number</c>), with exactly 9 digits.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the location of the candidate (<c>location_id</c>), used to compute the distance.
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Gets or sets the desired role (<c>desired_role</c>).
    /// </summary>
    public string? DesiredRole { get; set; }

    /// <summary>
    /// Gets or sets the summary of the experience (<c>experience_summary</c>), with at most 1000 characters.
    /// </summary>
    public string? ExperienceSummary { get; set; }

    /// <summary>
    /// Gets or sets the availability (<c>availability</c>).
    /// </summary>
    public Availability? Availability { get; set; }

    /// <summary>
    /// Gets or sets the path of the optional profile photo (<c>profile_photo_path</c>).
    /// </summary>
    public string? ProfilePhotoPath { get; set; }

    /// <summary>
    /// Gets or sets the path of the optional curriculum vitae (<c>cv_path</c>).
    /// </summary>
    public string? CvPath { get; set; }

    /// <summary>
    /// Gets or sets the maximum distance in kilometres (<c>max_distance_km</c>), from 1 to 500; <see langword="null"/>
    /// while undefined, in which case the distance filter is not applied.
    /// </summary>
    public int? MaxDistanceKm { get; set; }

    /// <summary>
    /// Gets or sets the minimum salary expectation in gross euros per month (<c>min_salary_expectation</c>);
    /// <see langword="null"/> means no salary filter.
    /// </summary>
    public decimal? MinSalaryExpectation { get; set; }

    /// <summary>
    /// Gets or sets the preferred work modes (<c>preferred_work_modes</c>); an empty list means all the modes.
    /// </summary>
    public List<WorkMode> PreferredWorkModes { get; set; } = [];

    /// <summary>
    /// Gets or sets the preferred contract types (<c>preferred_contract_types</c>); an empty list means all the types.
    /// </summary>
    public List<ContractType> PreferredContractTypes { get; set; } = [];

    /// <summary>
    /// Gets the interests still available in the quota (<c>swipe_rights_remaining</c>), from 0 to 10.
    /// </summary>
    public short SwipeRightsRemaining { get; private set; }

    /// <summary>
    /// Gets the end of the block period (<c>next_swipe_available_at</c>), which is only set with the quota at 0.
    /// </summary>
    public DateTimeOffset? NextSwipeAvailableAt { get; private set; }

    /// <summary>
    /// Gets the optimistic concurrency token, mapped to the <c>xmin</c> system column.
    /// </summary>
    public uint Version { get; private set; }

    /// <summary>
    /// Gets the professional links of the candidate, ordered by position.
    /// </summary>
    public IEnumerable<CandidateLink> Links => this.links.OrderBy(link => link.Position);

    /// <summary>
    /// Indica se o Candidato pode manifestar um novo interesse: há interesses na quota ou o período de bloqueio terminou
    /// (RF020).
    /// </summary>
    /// <param name="now">Instante da consulta, em UTC.</param>
    /// <returns><see langword="true"/> se um interesse puder ser registado.</returns>
    public bool HasInterestAvailable(DateTimeOffset now)
    {
        return this.SwipeRightsRemaining > 0 || this.IsBlockOver(now);
    }

    /// <summary>
    /// Consome um interesse da quota. O interesse que a esgota inicia o período de bloqueio de 24 horas (RF020, P11).
    /// Se o período anterior já terminou, a quota é reposta antes de consumir (RF021).
    /// </summary>
    /// <param name="now">Instante do interesse, em UTC.</param>
    /// <exception cref="InvalidOperationException">Se a quota estiver esgotada e o período de bloqueio não tiver terminado.</exception>
    public void ConsumeInterest(DateTimeOffset now)
    {
        this.RestoreQuotaIfDue(now);

        if (this.SwipeRightsRemaining == 0)
        {
            throw new InvalidOperationException("A quota de interesses está esgotada até ao fim do período de bloqueio.");
        }

        this.SwipeRightsRemaining--;

        if (this.SwipeRightsRemaining == 0)
        {
            this.NextSwipeAvailableAt = now + BlockPeriod;
        }
    }

    /// <summary>
    /// Repõe a quota na totalidade se o período de bloqueio terminou; nunca repõe parcialmente nem antes de a quota estar
    /// esgotada (RF021).
    /// </summary>
    /// <param name="now">Instante da verificação, em UTC.</param>
    /// <returns><see langword="true"/> se a quota foi reposta.</returns>
    public bool RestoreQuotaIfDue(DateTimeOffset now)
    {
        if (!this.IsBlockOver(now))
        {
            return false;
        }

        this.SwipeRightsRemaining = MaxInterests;
        this.NextSwipeAvailableAt = null;
        return true;
    }

    /// <summary>
    /// Substitui as hiperligações profissionais pelas indicadas, por ordem: a primeira fica na posição 1 (RF012, P10).
    /// As que não mudaram de posição nem de endereço ficam como estão; as outras são substituídas e as que sobram são
    /// removidas.
    /// </summary>
    /// <param name="urls">Até três endereços diferentes, iniciados por <c>http://</c> ou <c>https://</c>.</param>
    /// <exception cref="ArgumentException">Se houver mais de três endereços, repetidos, vazios ou com formato inválido.</exception>
    public void SetLinks(IReadOnlyList<string> urls)
    {
        ArgumentNullException.ThrowIfNull(urls);

        if (urls.Count > MaxLinks)
        {
            throw new ArgumentException($"Cada Candidato tem no máximo {MaxLinks} hiperligações.", nameof(urls));
        }

        foreach (string url in urls)
        {
            if (string.IsNullOrEmpty(url) || url.Length > 500 || !LinkPattern().IsMatch(url))
            {
                throw new ArgumentException("A hiperligação deve começar por http:// ou https:// e ter até 500 caracteres.", nameof(urls));
            }
        }

        if (urls.Distinct(StringComparer.Ordinal).Count() != urls.Count)
        {
            throw new ArgumentException("As hiperligações não podem estar repetidas.", nameof(urls));
        }

        // As hiperligações lidas da base de dados não vêm ordenadas (um UPDATE no PostgreSQL muda a ordem física das
        // linhas), por isso cada uma é procurada pela posição e nunca pelo índice na lista.
        for (int index = 0; index < urls.Count; index++)
        {
            short position = (short)(index + 1);
            CandidateLink? existing = this.links.Find(link => link.Position == position);

            if (existing is not null && string.Equals(existing.Url, urls[index], StringComparison.Ordinal))
            {
                continue;
            }

            // Uma hiperligação com outro endereço é substituída por uma nova (apagar e inserir) e não atualizada:
            // o EF Core ordena os apagamentos antes das inserções, o que permite remover a primeira ou trocar duas
            // sem violar os índices únicos de posição e de endereço.
            if (existing is not null)
            {
                this.links.Remove(existing);
            }

            this.links.Add(new CandidateLink(this.Id, position, urls[index]));
        }

        this.links.RemoveAll(link => link.Position > urls.Count);
    }

    [GeneratedRegex(@"^[0-9]{9}\z")]
    private static partial Regex PhoneNumberPattern();

    [GeneratedRegex("^https?://", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();

    private bool IsBlockOver(DateTimeOffset now)
    {
        return this.NextSwipeAvailableAt is { } end && now >= end;
    }
}
