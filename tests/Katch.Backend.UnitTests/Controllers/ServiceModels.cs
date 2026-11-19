// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Services.Models;

namespace Katch.Backend.UnitTests.Controllers;

/// <summary>
/// Modelos da camada de serviços com valores fictícios, devolvidos pelos duplos dos serviços nos testes dos controllers.
/// Cada propriedade cria uma instância nova; os testes comparam a resposta do controller com a conversão do modelo
/// para o formato da API (I138).
/// </summary>
public static class ServiceModels
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 18, 10, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Gets a session of a Candidate.
    /// </summary>
    public static LoginResult Session => new("token.fictício", Moment.AddHours(1), UserType.Candidate, Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"));

    /// <summary>
    /// Gets search preferences with every field filled.
    /// </summary>
    public static SearchPreferencesModel Preferences => new(25, 1200m, [WorkMode.Hybrid, WorkMode.Remote], [ContractType.Permanent]);

    /// <summary>
    /// Gets the profile of a Candidate with skills, custom skills and links.
    /// </summary>
    public static CandidateProfileModel CandidateProfile => new(
        Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
        "Ana Exemplo",
        "ana@exemplo.pt",
        "912345678",
        Guid.Parse("1b4e28ba-2fa1-11d2-883f-0016d3cca427"),
        "Porto",
        "Programadora",
        Availability.OneMonth,
        "Três anos em aplicações Web.",
        "/api/files/PROFILE_PHOTO/a.png",
        true,
        ["https://a.pt"],
        [new ReferenceItemModel(Guid.Parse("6fa459ea-ee8a-3ca4-894e-db77e160355e"), "C#")],
        [new CustomSkillModel(Guid.Parse("886313e1-3b8a-5372-9b90-0c9aee199e5d"), "Kotlin")],
        Preferences);

    /// <summary>
    /// Gets the state of a pending registration of a Company.
    /// </summary>
    public static CompanyStatusModel RegistrationStatus => new(Guid.Parse("a8098c1a-f86e-11da-bd1a-00112444be1e"), CompanyStatus.Pending, null, Moment);

    /// <summary>
    /// Gets an approved Company with the page, the logo and one photo of the gallery, whose page the services return.
    /// </summary>
    public static Company Company
    {
        get
        {
            Company company = ApprovedCompany;
            company.UpdatePage("Software de gestão de frotas.", "https://www.lumen-exemplo.pt");
            company.SetLogo("company_logo/0123456789abcdef0123456789abcdef.png");
            company.AddGalleryPhoto("company_photo/11111111111111111111111111111111.jpg");
            return company;
        }
    }

    /// <summary>
    /// Gets an approved Company without the page, the logo and the photos of the gallery.
    /// </summary>
    public static Company ApprovedCompany
    {
        get
        {
            var company = new Company(
                Guid.Parse("9b2c4d6e-8f10-4a2b-9c3d-5e6f7a8b9c0d"),
                new CompanyRegistration
                {
                    CompanyName = "Lumen Software, Lda.",
                    TaxId = "509123456",
                    Industry = Industry.Technology,
                    Address = "Rua do Exemplo, 1",
                    LocationId = Guid.Parse("1b4e28ba-2fa1-11d2-883f-0016d3cca427"),
                    ContactEmail = "geral@lumen-exemplo.pt",
                    ContactPhone = "221234567",
                    ResponsibleName = "Rui Exemplo",
                },
                Moment);
            company.Approve();
            return company;
        }
    }

    /// <summary>
    /// Gets the data of the registration of a Company.
    /// </summary>
    public static CompanyRegistrationModel CompanyRegistration => new(
        Guid.Parse("a8098c1a-f86e-11da-bd1a-00112444be1e"),
        "Lumen Software, Lda.",
        "500000000",
        Industry.Technology,
        "Rua do Exemplo, 1",
        Guid.Parse("1b4e28ba-2fa1-11d2-883f-0016d3cca427"),
        "Porto",
        "geral@lumen-exemplo.pt",
        "221234567",
        "Rui Exemplo",
        Moment);

    /// <summary>
    /// Gets the indicators of a period.
    /// </summary>
    public static IndicatorsModel Indicators => new(10, 4, 2, 7, 30, 5, 3);

    /// <summary>
    /// Gets the card of a published Job Offer.
    /// </summary>
    public static JobCardModel JobCard => new(
        Guid.Parse("e902893a-9d22-3c7e-a7b8-d6e313b71d9f"),
        "Programador .NET",
        "Lumen Software, Lda.",
        "/api/files/COMPANY_LOGO/l.png",
        1200m,
        1800m,
        "Porto",
        12,
        ContractType.Permanent,
        WorkMode.Hybrid,
        true,
        ["/api/files/JOB_PHOTO/f.png"]);

    /// <summary>
    /// Gets the detail of a published Job Offer.
    /// </summary>
    public static JobDetailModel JobDetail => new(JobCard, "Desenvolvimento da API.", ["C#"], ["Seguro de saúde"], Guid.Parse("a8098c1a-f86e-11da-bd1a-00112444be1e"));

    /// <summary>
    /// Gets the quota of interests of a Candidate.
    /// </summary>
    public static QuotaModel Quota => new(7, 10, null);

    /// <summary>
    /// Gets a Job Offer of the Company of the Recruiter.
    /// </summary>
    public static JobModel Job => new(
        Guid.Parse("e902893a-9d22-3c7e-a7b8-d6e313b71d9f"),
        "Programador .NET",
        "Desenvolvimento da API.",
        1200m,
        1800m,
        Guid.Parse("1b4e28ba-2fa1-11d2-883f-0016d3cca427"),
        "Porto",
        ContractType.Permanent,
        WorkMode.Hybrid,
        [new ReferenceItemModel(Guid.Parse("6fa459ea-ee8a-3ca4-894e-db77e160355e"), "C#")],
        [new ReferenceItemModel(Guid.Parse("16fd2706-8baf-433b-82eb-8c7fada847da"), "Seguro de saúde")],
        false,
        Moment.AddDays(30),
        JobStatus.Published,
        Moment,
        ["/api/files/JOB_PHOTO/f.png"],
        2);

    /// <summary>
    /// Gets the full profile of a Candidate seen by the Recruiter.
    /// </summary>
    public static CandidateFullProfileModel CandidateFullProfile => new(
        Guid.Parse("21ec2020-3aea-4069-a2dd-08002b30309d"),
        "Ana Exemplo",
        null,
        "Programadora",
        "Porto",
        Availability.Immediate,
        null,
        ["https://a.pt"],
        [new SkillTagModel("C#", true), new SkillTagModel("Kotlin", false)],
        false,
        Moment);

    /// <summary>
    /// Gets a confirmed Match.
    /// </summary>
    public static MatchModel Match => new(
        Guid.Parse("21ec2020-3aea-4069-a2dd-08002b30309d"),
        "Programador .NET",
        JobStatus.Published,
        "Porto",
        "Lumen Software, Lda.",
        null,
        "Ana Exemplo",
        "ana@exemplo.pt",
        "912345678",
        Moment);

    /// <summary>
    /// Gets a message of a conversation.
    /// </summary>
    public static MessageModel Message => new(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"), "Olá!", Moment, null);
}
