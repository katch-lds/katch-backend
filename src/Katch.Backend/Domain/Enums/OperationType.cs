// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Domain.Enums;

/// <summary>
/// Operação registada no registo de autoria (RF112). Tipo PostgreSQL <c>operation_type</c>.
/// </summary>
public enum OperationType
{
    /// <summary>
    /// Registo da Empresa submetido.
    /// </summary>
    CompanySubmitted,

    /// <summary>
    /// Dados da Empresa alterados.
    /// </summary>
    CompanyUpdated,

    /// <summary>
    /// Empresa aprovada.
    /// </summary>
    CompanyApproved,

    /// <summary>
    /// Empresa recusada.
    /// </summary>
    CompanyRejected,

    /// <summary>
    /// Empresa suspensa.
    /// </summary>
    CompanySuspended,

    /// <summary>
    /// Empresa reativada.
    /// </summary>
    CompanyReactivated,

    /// <summary>
    /// Vaga criada.
    /// </summary>
    JobCreated,

    /// <summary>
    /// Vaga publicada.
    /// </summary>
    JobPublished,

    /// <summary>
    /// Vaga alterada.
    /// </summary>
    JobUpdated,

    /// <summary>
    /// Vaga suspensa.
    /// </summary>
    JobSuspended,

    /// <summary>
    /// Vaga novamente publicada.
    /// </summary>
    JobRepublished,

    /// <summary>
    /// Vaga encerrada; sem autor quando encerrada pela tarefa periódica.
    /// </summary>
    JobClosed,

    /// <summary>
    /// Vaga eliminada.
    /// </summary>
    JobDeleted,

    /// <summary>
    /// Conta bloqueada.
    /// </summary>
    AccountBlocked,

    /// <summary>
    /// Conta reativada.
    /// </summary>
    AccountReactivated,

    /// <summary>
    /// Conta suspensa.
    /// </summary>
    AccountSuspended,

    /// <summary>
    /// Competência acrescentada à lista.
    /// </summary>
    SkillCreated,

    /// <summary>
    /// Designação de competência alterada.
    /// </summary>
    SkillRenamed,

    /// <summary>
    /// Benefício acrescentado à lista.
    /// </summary>
    BenefitCreated,

    /// <summary>
    /// Designação de benefício alterada.
    /// </summary>
    BenefitRenamed,
}
