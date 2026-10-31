// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;

namespace Katch.Backend.Services;

/// <summary>
/// Consulta e manutenção das listas pré-definidas (modelo de classes do backend v02, secção 5.1). A consulta das
/// competências é acrescentada pela Issue da gestão das competências (I062).
/// </summary>
public interface IReferenceListService
{
    /// <summary>
    /// Lista as localidades por ordem alfabética da designação «Localidade (Distrito)», sem coordenadas (documentação
    /// da API v02, secção 6.13.1).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>As localidades.</returns>
    public Task<List<ReferenceItemDto>> ListLocationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista os benefícios por ordem alfabética da designação (documentação da API v02, secção 6.13.3; RF050).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>Os benefícios.</returns>
    public Task<List<ReferenceItemDto>> ListBenefitsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta uma competência à lista e regista a operação (documentação da API v02, secção 6.13.4; RF099, RF112).
    /// </summary>
    /// <param name="adminId">Identificador do Administrador da credencial de sessão.</param>
    /// <param name="request">Designação da competência.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A competência criada.</returns>
    /// <exception cref="ValidationFailedException">Se a designação estiver vazia, for demasiado longa ou já existir.</exception>
    public Task<ReferenceItemDto> AddSkillAsync(
        Guid adminId,
        ReferenceItemRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Altera a designação de uma competência e regista a operação; os perfis e as vagas que a usam mantêm a associação e
    /// passam a apresentar a nova designação (documentação da API v02, secção 6.13.5; RF100, RF112).
    /// </summary>
    /// <param name="adminId">Identificador do Administrador da credencial de sessão.</param>
    /// <param name="skillId">Identificador da competência.</param>
    /// <param name="request">Nova designação.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>A competência alterada.</returns>
    /// <exception cref="ValidationFailedException">Se a designação estiver vazia, for demasiado longa ou já existir noutra competência.</exception>
    /// <exception cref="ElementNotFoundException">Se a competência não existir.</exception>
    public Task<ReferenceItemDto> RenameSkillAsync(
        Guid adminId,
        Guid skillId,
        ReferenceItemRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta um benefício à lista e regista a operação (documentação da API v02, secção 6.13.6; RF101, RF112).
    /// </summary>
    /// <param name="adminId">Identificador do Administrador da credencial de sessão.</param>
    /// <param name="request">Designação do benefício.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O benefício criado.</returns>
    /// <exception cref="ValidationFailedException">Se a designação estiver vazia, for demasiado longa ou já existir.</exception>
    public Task<ReferenceItemDto> AddBenefitAsync(
        Guid adminId,
        ReferenceItemRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Altera a designação de um benefício e regista a operação; as vagas que o usam mantêm a associação e passam a
    /// apresentar a nova designação (documentação da API v02, secção 6.13.7; RF102, RF112).
    /// </summary>
    /// <param name="adminId">Identificador do Administrador da credencial de sessão.</param>
    /// <param name="benefitId">Identificador do benefício.</param>
    /// <param name="request">Nova designação.</param>
    /// <param name="cancellationToken">Cancelamento do pedido.</param>
    /// <returns>O benefício alterado.</returns>
    /// <exception cref="ValidationFailedException">Se a designação estiver vazia, for demasiado longa ou já existir noutro benefício.</exception>
    /// <exception cref="ElementNotFoundException">Se o benefício não existir.</exception>
    public Task<ReferenceItemDto> RenameBenefitAsync(
        Guid adminId,
        Guid benefitId,
        ReferenceItemRequest request,
        CancellationToken cancellationToken = default);
}
