// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services.Models;

namespace Katch.Backend.Services;

/// <summary>
/// Erros do catálogo da API (documentação da API v02, secção 4.3) que os serviços indicam em
/// <see cref="OperationConflictException"/>. Os códigos e as mensagens são os de <see cref="ApiErrorResponse"/>; a
/// conversão no corpo da resposta é feita na camada da API (I138).
/// </summary>
public static class ServiceErrors
{
    /// <summary>
    /// Cria o ERR-30: a operação não é permitida no estado atual do elemento.
    /// </summary>
    /// <param name="stateLabel">Estado atual, como é apresentado ao utilizador (por exemplo «pendente»).</param>
    /// <returns>O erro, por exemplo «A operação não é permitida no estado atual: pendente.».</returns>
    public static ServiceError InvalidState(string stateLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateLabel);

        return new ServiceError(ApiErrorResponse.InvalidStateCode, $"A operação não é permitida no estado atual: {stateLabel}.");
    }

    /// <summary>
    /// Cria o ERR-34: a aceitação ou a recusa foi pedida sem a abertura do perfil completo do Candidato registada para
    /// a vaga (RF063, RF064; UC12, E1).
    /// </summary>
    /// <returns>O erro.</returns>
    public static ServiceError ProfileNotOpened()
    {
        return new ServiceError(ApiErrorResponse.ProfileNotOpenedCode, ApiErrorResponse.ProfileNotOpenedMessage);
    }

    /// <summary>
    /// Cria o ERR-35: o interesse já não está em espera, porque o Recrutador já registou a decisão (RF117; UC12, E4).
    /// </summary>
    /// <returns>O erro.</returns>
    public static ServiceError CandidateAlreadyEvaluated()
    {
        return new ServiceError(ApiErrorResponse.CandidateAlreadyEvaluatedCode, ApiErrorResponse.CandidateAlreadyEvaluatedMessage);
    }

    /// <summary>
    /// Cria o ERR-36: a conversa está em modo apenas de consulta (RF028, RF075).
    /// </summary>
    /// <returns>O erro.</returns>
    public static ServiceError ConversationReadOnly()
    {
        return new ServiceError(ApiErrorResponse.ConversationReadOnlyCode, ApiErrorResponse.ConversationReadOnlyMessage);
    }

    /// <summary>
    /// Cria o ERR-37: o identificador não corresponde a um match confirmado (RF026).
    /// </summary>
    /// <returns>O erro.</returns>
    public static ServiceError ConversationWithoutMatch()
    {
        return new ServiceError(ApiErrorResponse.ConversationWithoutMatchCode, ApiErrorResponse.ConversationWithoutMatchMessage);
    }

    /// <summary>
    /// Cria o ERR-38: a vaga tem pelo menos um interesse registado e só pode ser encerrada (RF058).
    /// </summary>
    /// <returns>O erro.</returns>
    public static ServiceError JobHasInterests()
    {
        return new ServiceError(ApiErrorResponse.JobHasInterestsCode, ApiErrorResponse.JobHasInterestsMessage);
    }

    /// <summary>
    /// Cria o ERR-39: a galeria ou a vaga já tem o número máximo de fotografias (RF048, RF052, P13).
    /// </summary>
    /// <param name="maxPhotos">Número máximo de fotografias, indicado na mensagem.</param>
    /// <returns>O erro.</returns>
    public static ServiceError PhotoLimitReached(int maxPhotos)
    {
        return new ServiceError(ApiErrorResponse.PhotoLimitReachedCode, $"Foi atingido o número máximo de fotografias ({maxPhotos}).");
    }

    /// <summary>
    /// Cria o ERR-40: o Recrutador já registou uma Empresa.
    /// </summary>
    /// <returns>O erro.</returns>
    public static ServiceError CompanyAlreadyRegistered()
    {
        return new ServiceError(ApiErrorResponse.CompanyAlreadyRegisteredCode, ApiErrorResponse.CompanyAlreadyRegisteredMessage);
    }

    /// <summary>
    /// Cria o ERR-41: falha do controlo de concorrência otimista (<c>xmin</c>).
    /// </summary>
    /// <returns>O erro.</returns>
    public static ServiceError ConcurrencyConflict()
    {
        return new ServiceError(ApiErrorResponse.ConcurrencyConflictCode, ApiErrorResponse.ConcurrencyConflictMessage);
    }
}
