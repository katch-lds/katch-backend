// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using System.Text.RegularExpressions;
using Katch.Backend.Domain.Enums;

namespace Katch.Backend.Domain;

/// <summary>
/// Empresa representada por um único Recrutador (tabela <c>company</c>), com os dados de registo, o estado de aprovação
/// e a página de apresentação (logótipo, descrição, site na Internet e galeria).
/// </summary>
/// <remarks>
/// A entidade aplica as regras que dependem só da própria linha (modelo de dados v02, secção 7.1; modelo de classes do
/// backend v02, secção 3.1, e decisão DC-03): formatos dos dados de registo, galeria até 6 fotografias, motivo de 10 a
/// 500 caracteres só no estado <see cref="CompanyStatus.Rejected"/> e transições de estado da documentação da API v02
/// (secção 6.11). A unicidade do NIF e do Recrutador depende de outras linhas e é garantida pela base de dados e pelos
/// serviços. A autoria e a data das mudanças de estado ficam no <c>OperationLog</c>, registadas pelos serviços (DM-18),
/// e a entidade não tem datas de criação nem de alteração (DM-22).
/// </remarks>
public sealed partial class Company
{
    /// <summary>
    /// Número máximo de fotografias da galeria (P13).
    /// </summary>
    public const int MaxGalleryPhotos = 6;

    /// <summary>
    /// Comprimento mínimo do motivo da recusa, sem os espaços nas extremidades (P12).
    /// </summary>
    public const int MinReasonLength = 10;

    /// <summary>
    /// Comprimento máximo do motivo da recusa (P12).
    /// </summary>
    public const int MaxReasonLength = 500;

    /// <summary>
    /// Comprimento máximo da descrição da atividade (P08).
    /// </summary>
    public const int MaxDescriptionLength = 1000;

    /// <summary>
    /// Comprimento máximo do site na Internet e dos caminhos dos ficheiros (DM-12).
    /// </summary>
    public const int MaxPathLength = 500;

    /// <summary>
    /// Initializes a new instance of the <see cref="Company"/> class no estado <see cref="CompanyStatus.Pending"/>,
    /// com a data de submissão do pedido (RF041, RF043).
    /// </summary>
    /// <param name="recruiterId">Identificador do Recrutador que representa a Empresa.</param>
    /// <param name="data">Dados de registo da Empresa.</param>
    /// <param name="now">Instante da submissão, em UTC.</param>
    /// <exception cref="ArgumentException">
    /// Se o identificador do Recrutador estiver vazio ou se algum dado de registo não respeitar o modelo de dados.
    /// </exception>
    public Company(Guid recruiterId, CompanyRegistration data, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (recruiterId == Guid.Empty)
        {
            throw new ArgumentException("O identificador do Recrutador é obrigatório.", nameof(recruiterId));
        }

        ValidateRegistration(data);

        this.Id = Guid.NewGuid();
        this.RecruiterId = recruiterId;
        this.CompanyName = string.Empty;
        this.TaxId = string.Empty;
        this.Address = string.Empty;
        this.ContactEmail = string.Empty;
        this.ContactPhone = string.Empty;
        this.ResponsibleName = string.Empty;
        this.ApplyRegistration(data);
        this.Status = CompanyStatus.Pending;
        this.SubmittedAt = now;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Company"/> class. Usado pelo Entity Framework Core.
    /// </summary>
    private Company()
    {
        this.CompanyName = string.Empty;
        this.TaxId = string.Empty;
        this.Address = string.Empty;
        this.ContactEmail = string.Empty;
        this.ContactPhone = string.Empty;
        this.ResponsibleName = string.Empty;
    }

    /// <summary>
    /// Gets the identifier of the company (<c>id</c>).
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the identifier of the recruiter that represents the company (<c>recruiter_id</c>), unique.
    /// </summary>
    public Guid RecruiterId { get; private set; }

    /// <summary>
    /// Gets the registered name of the company (<c>company_name</c>).
    /// </summary>
    public string CompanyName { get; private set; }

    /// <summary>
    /// Gets the tax identification number (<c>tax_id</c>), with 9 digits, unique and not changeable after the approval.
    /// </summary>
    public string TaxId { get; private set; }

    /// <summary>
    /// Gets the industry of the company (<c>industry</c>).
    /// </summary>
    public Industry Industry { get; private set; }

    /// <summary>
    /// Gets the address (<c>address</c>).
    /// </summary>
    public string Address { get; private set; }

    /// <summary>
    /// Gets the identifier of the location of the company (<c>location_id</c>), proposed by default in the jobs.
    /// </summary>
    public Guid LocationId { get; private set; }

    /// <summary>
    /// Gets the contact e-mail address (<c>contact_email</c>), visible to the candidate only after the match.
    /// </summary>
    public string ContactEmail { get; private set; }

    /// <summary>
    /// Gets the contact phone number (<c>contact_phone</c>), with 9 digits, visible to the candidate only after the match.
    /// </summary>
    public string ContactPhone { get; private set; }

    /// <summary>
    /// Gets the name of the person responsible for the company (<c>responsible_name</c>).
    /// </summary>
    public string ResponsibleName { get; private set; }

    /// <summary>
    /// Gets the single website address (<c>website</c>), starting with <c>http://</c> or <c>https://</c>.
    /// </summary>
    public string? Website { get; private set; }

    /// <summary>
    /// Gets the description of the activity (<c>description</c>), with at most 1000 characters.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the path of the logo (<c>logo_path</c>).
    /// </summary>
    public string? LogoPath { get; private set; }

    /// <summary>
    /// Gets the paths of the gallery photos (<c>photo_paths</c>), at most 6; the order of the list is the order in
    /// which they are shown.
    /// </summary>
    public List<string> PhotoPaths { get; private set; } = [];

    /// <summary>
    /// Gets the state of the registration (<c>status</c>).
    /// </summary>
    public CompanyStatus Status { get; private set; }

    /// <summary>
    /// Gets the reason for the rejection of the registration (<c>status_reason</c>), set only in the
    /// <see cref="CompanyStatus.Rejected"/> state.
    /// </summary>
    public string? StatusReason { get; private set; }

    /// <summary>
    /// Gets the date of submission of the request (<c>submitted_at</c>), updated in each new submission.
    /// </summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>
    /// Indica se a Empresa está aprovada, condição das operações reservadas do Recrutador (RF039).
    /// </summary>
    /// <returns><see langword="true"/> se o estado for <see cref="CompanyStatus.Approved"/>.</returns>
    public bool IsApproved()
    {
        return this.Status == CompanyStatus.Approved;
    }

    /// <summary>
    /// Altera os dados de registo da Empresa aprovada, que se mantém aprovada (RF049). O NIF não pode ser alterado
    /// depois da aprovação.
    /// </summary>
    /// <param name="data">Novos dados de registo, com o mesmo NIF.</param>
    /// <exception cref="InvalidOperationException">Se a Empresa não estiver aprovada.</exception>
    /// <exception cref="ArgumentException">Se o NIF for diferente do registado ou se algum dado for inválido.</exception>
    public void UpdateRegistration(CompanyRegistration data)
    {
        ArgumentNullException.ThrowIfNull(data);
        this.EnsureApproved();
        ValidateRegistration(data);

        if (!string.Equals(data.TaxId, this.TaxId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "O número de identificação fiscal não pode ser alterado depois da aprovação.",
                nameof(data));
        }

        this.ApplyRegistration(data);
    }

    /// <summary>
    /// Corrige os dados de um pedido recusado e submete-o novamente: a Empresa volta a pendente, o motivo da recusa é
    /// limpo e a data de submissão é atualizada (RF045). O NIF pode ser corrigido, porque a Empresa nunca foi aprovada.
    /// </summary>
    /// <param name="data">Dados de registo corrigidos.</param>
    /// <param name="now">Instante da nova submissão, em UTC.</param>
    /// <exception cref="InvalidOperationException">Se a Empresa não estiver recusada.</exception>
    /// <exception cref="ArgumentException">Se algum dado de registo for inválido.</exception>
    public void Resubmit(CompanyRegistration data, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);
        this.EnsureStatus(CompanyStatus.Rejected, "Só um pedido recusado pode ser submetido novamente.");
        ValidateRegistration(data);

        this.ApplyRegistration(data);
        this.Status = CompanyStatus.Pending;
        this.StatusReason = null;
        this.SubmittedAt = now;
    }

    /// <summary>
    /// Aprova o registo de uma Empresa pendente (RF093).
    /// </summary>
    /// <exception cref="InvalidOperationException">Se a Empresa não estiver pendente.</exception>
    public void Approve()
    {
        this.EnsureStatus(CompanyStatus.Pending, "Só uma Empresa pendente pode ser aprovada.");

        this.Status = CompanyStatus.Approved;
    }

    /// <summary>
    /// Recusa o registo de uma Empresa pendente, com o motivo indicado (RF094, P12). O motivo é guardado sem os espaços
    /// nas extremidades.
    /// </summary>
    /// <param name="reason">Motivo da recusa, com 10 a 500 caracteres.</param>
    /// <exception cref="InvalidOperationException">Se a Empresa não estiver pendente.</exception>
    /// <exception cref="ArgumentException">Se o motivo estiver em falta ou fora dos 10 a 500 caracteres.</exception>
    public void Reject(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        this.EnsureStatus(CompanyStatus.Pending, "Só uma Empresa pendente pode ser recusada.");

        string trimmed = reason.Trim();

        if (trimmed.Length < MinReasonLength || trimmed.Length > MaxReasonLength)
        {
            throw new ArgumentException(
                $"O motivo da recusa deve ter entre {MinReasonLength} e {MaxReasonLength} caracteres.",
                nameof(reason));
        }

        this.Status = CompanyStatus.Rejected;
        this.StatusReason = trimmed;
    }

    /// <summary>
    /// Suspende uma Empresa aprovada (RF095). O encerramento das conversas abertas fica no serviço (RF075).
    /// </summary>
    /// <exception cref="InvalidOperationException">Se a Empresa não estiver aprovada.</exception>
    public void Suspend()
    {
        this.EnsureStatus(CompanyStatus.Approved, "Só uma Empresa aprovada pode ser suspensa.");

        this.Status = CompanyStatus.Suspended;
    }

    /// <summary>
    /// Reativa uma Empresa suspensa, que volta a aprovada (RF116).
    /// </summary>
    /// <exception cref="InvalidOperationException">Se a Empresa não estiver suspensa.</exception>
    public void Reactivate()
    {
        this.EnsureStatus(CompanyStatus.Suspended, "Só uma Empresa suspensa pode ser reativada.");

        this.Status = CompanyStatus.Approved;
    }

    /// <summary>
    /// Grava a descrição da atividade e o sítio na Internet da página de apresentação (RF046). Um valor nulo ou só com
    /// espaços limpa o campo.
    /// </summary>
    /// <param name="description">Descrição da atividade, com no máximo 1000 caracteres (P08).</param>
    /// <param name="website">Endereço iniciado por <c>http://</c> ou <c>https://</c>, com no máximo 500 caracteres (P10).</param>
    /// <exception cref="InvalidOperationException">Se a Empresa não estiver aprovada.</exception>
    /// <exception cref="ArgumentException">Se a descrição for demasiado longa ou o endereço for inválido.</exception>
    public void UpdatePage(string? description, string? website)
    {
        this.EnsureApproved();

        string? newDescription = string.IsNullOrWhiteSpace(description) ? null : description;
        string? newWebsite = string.IsNullOrWhiteSpace(website) ? null : website;

        if (newDescription is not null && newDescription.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                $"A descrição tem no máximo {MaxDescriptionLength} caracteres.",
                nameof(description));
        }

        if (newWebsite is not null && (newWebsite.Length > MaxPathLength || !WebsitePattern().IsMatch(newWebsite)))
        {
            throw new ArgumentException(
                $"O sítio na Internet deve começar por http:// ou https:// e ter até {MaxPathLength} caracteres.",
                nameof(website));
        }

        this.Description = newDescription;
        this.Website = newWebsite;
    }

    /// <summary>
    /// Define o caminho do logótipo, substituindo o anterior (RF047).
    /// </summary>
    /// <param name="path">Caminho do ficheiro guardado, com no máximo 500 caracteres.</param>
    /// <exception cref="InvalidOperationException">Se a Empresa não estiver aprovada.</exception>
    /// <exception cref="ArgumentException">Se o caminho estiver vazio ou for demasiado longo.</exception>
    public void SetLogo(string path)
    {
        this.EnsureApproved();
        ValidatePath(path);

        this.LogoPath = path;
    }

    /// <summary>
    /// Acrescenta uma fotografia no fim da galeria (RF048, P13).
    /// </summary>
    /// <param name="path">Caminho do ficheiro guardado, com no máximo 500 caracteres.</param>
    /// <exception cref="InvalidOperationException">Se a Empresa não estiver aprovada ou a galeria já tiver 6 fotografias.</exception>
    /// <exception cref="ArgumentException">Se o caminho estiver vazio ou for demasiado longo.</exception>
    public void AddGalleryPhoto(string path)
    {
        this.EnsureApproved();
        ValidatePath(path);

        if (this.PhotoPaths.Count >= MaxGalleryPhotos)
        {
            throw new InvalidOperationException($"A galeria tem no máximo {MaxGalleryPhotos} fotografias.");
        }

        this.PhotoPaths.Add(path);
    }

    private static void ValidateRegistration(CompanyRegistration data)
    {
        ArgumentNullException.ThrowIfNull(data);

        ValidateText(data.CompanyName, 200, nameof(data.CompanyName));
        ValidateText(data.Address, 255, nameof(data.Address));
        ValidateText(data.ResponsibleName, 150, nameof(data.ResponsibleName));

        if (data.TaxId is null || !NineDigitsPattern().IsMatch(data.TaxId))
        {
            throw new ArgumentException("O número de identificação fiscal deve ter exatamente 9 algarismos.", nameof(data));
        }

        if (data.ContactPhone is null || !NineDigitsPattern().IsMatch(data.ContactPhone))
        {
            throw new ArgumentException("O telefone de contacto deve ter exatamente 9 algarismos.", nameof(data));
        }

        if (!Enum.IsDefined(data.Industry))
        {
            throw new ArgumentException("O setor de atividade não pertence à lista pré-definida.", nameof(data));
        }

        if (!IsValidEmail(data.ContactEmail))
        {
            throw new ArgumentException(
                "O correio eletrónico de contacto deve ter o formato local@dominio e até 255 caracteres.",
                nameof(data));
        }
    }

    private static void ValidateText(string value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            throw new ArgumentException($"O campo {field} é obrigatório e tem no máximo {maxLength} caracteres.", field);
        }
    }

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(path.Length, MaxPathLength);
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrEmpty(email) || email.Length > 255)
        {
            return false;
        }

        // Mesma regra do CHECK ck_company_contact_email: exatamente um '@', com texto sem espaços de cada lado.
        int at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0
            && at == email.LastIndexOf('@')
            && at < email.Length - 1
            && !email.Any(char.IsWhiteSpace);
    }

    [GeneratedRegex(@"^[0-9]{9}\z")]
    private static partial Regex NineDigitsPattern();

    [GeneratedRegex("^https?://", RegexOptions.IgnoreCase)]
    private static partial Regex WebsitePattern();

    private void ApplyRegistration(CompanyRegistration data)
    {
        this.CompanyName = data.CompanyName;
        this.TaxId = data.TaxId;
        this.Industry = data.Industry;
        this.Address = data.Address;
        this.LocationId = data.LocationId;
        this.ContactEmail = data.ContactEmail;
        this.ContactPhone = data.ContactPhone;
        this.ResponsibleName = data.ResponsibleName;
    }

    private void EnsureApproved()
    {
        this.EnsureStatus(CompanyStatus.Approved, "A operação exige a Empresa aprovada.");
    }

    private void EnsureStatus(CompanyStatus expected, string message)
    {
        if (this.Status != expected)
        {
            throw new InvalidOperationException(message);
        }
    }
}
