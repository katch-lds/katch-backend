// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Dtos;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Services;
using Katch.Backend.Services.Models;

namespace Katch.Backend.UnitTests.Services;

/// <summary>
/// Testes dos erros de conflito indicados pela camada de serviços (<see cref="ServiceErrors"/>) e da sua conversão no
/// corpo da resposta da API (I138): os códigos e as mensagens são os do catálogo de erros da documentação da API v02,
/// secção 4, iguais aos que a API devolvia antes da separação das camadas.
/// </summary>
public sealed class ServiceErrorsTests
{
    /// <summary>
    /// Cada erro tem o código e a mensagem do catálogo.
    /// </summary>
    [Fact]
    public void ErrorsHaveTheCodeAndTheMessageOfTheCatalog()
    {
        Assert.Equal(new ServiceError("ERR-30", "A operação não é permitida no estado atual: publicada."), ServiceErrors.InvalidState("publicada"));
        Assert.Equal(
            new ServiceError("ERR-34", "Abra o perfil completo do Candidato antes de registar a decisão."),
            ServiceErrors.ProfileNotOpened());
        Assert.Equal(new ServiceError("ERR-35", "O Candidato já foi avaliado nesta vaga."), ServiceErrors.CandidateAlreadyEvaluated());
        Assert.Equal(new ServiceError("ERR-36", "A conversa está em modo apenas de consulta."), ServiceErrors.ConversationReadOnly());
        Assert.Equal(new ServiceError("ERR-37", "Não existe match para esta conversa."), ServiceErrors.ConversationWithoutMatch());
        Assert.Equal(
            new ServiceError("ERR-38", "A vaga tem interesses registados e só pode ser encerrada."),
            ServiceErrors.JobHasInterests());
        Assert.Equal(new ServiceError("ERR-39", "Foi atingido o número máximo de fotografias (5)."), ServiceErrors.PhotoLimitReached(5));
        Assert.Equal(new ServiceError("ERR-40", "O Recrutador já registou uma Empresa."), ServiceErrors.CompanyAlreadyRegistered());
        Assert.Equal(
            new ServiceError("ERR-41", "O elemento foi alterado por outro pedido. Atualize e tente novamente."),
            ServiceErrors.ConcurrencyConflict());
    }

    /// <summary>
    /// O estado em falta é recusado, em vez de produzir uma mensagem incompleta.
    /// </summary>
    /// <param name="label">Estado indicado.</param>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void InvalidStateRequiresTheLabel(string label)
    {
        Assert.Throws<ArgumentException>(() => ServiceErrors.InvalidState(label));
    }

    /// <summary>
    /// O erro do serviço passa à resposta da API com o mesmo código e a mesma mensagem, como os conflitos
    /// construídos antes na camada de serviços.
    /// </summary>
    [Fact]
    public void ServiceErrorBecomesTheApiErrorBody()
    {
        ApiErrorDto body = ApiErrorResponse.FromServiceError(ServiceErrors.ConversationReadOnly());

        Assert.Equivalent(new ApiErrorDto("ERR-36", "A conversa está em modo apenas de consulta."), body, strict: true);
        Assert.Throws<ArgumentNullException>(() => ApiErrorResponse.FromServiceError(null!));
    }
}
