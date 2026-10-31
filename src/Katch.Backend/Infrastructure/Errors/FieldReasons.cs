// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

namespace Katch.Backend.Infrastructure.Errors;

/// <summary>
/// Motivos fixos dos erros de campo (<c>fields[].reason</c>) do catálogo da documentação da API v02 (secção 4.3).
/// </summary>
public static class FieldReasons
{
    /// <summary>
    /// Campo obrigatório em falta ou vazio.
    /// </summary>
    public const string Required = "O campo é obrigatório.";

    /// <summary>
    /// Correio eletrónico fora do formato local@domínio (P04).
    /// </summary>
    public const string EmailFormat = "O endereço de correio eletrónico tem de ter o formato local@domínio.";

    /// <summary>
    /// Correio eletrónico já registado noutra conta, sem distinção de maiúsculas (RF004, RF037).
    /// </summary>
    public const string EmailTaken = "O endereço de correio eletrónico já está registado.";

    /// <summary>
    /// Correio eletrónico com mais de 255 caracteres (coluna <c>app_user.email</c>).
    /// </summary>
    public const string EmailTooLong = "O endereço de correio eletrónico tem no máximo 255 caracteres.";

    /// <summary>
    /// Contacto telefónico sem exatamente 9 algarismos (P04).
    /// </summary>
    public const string PhoneNumberFormat = "O contacto telefónico tem de ter 9 algarismos.";

    /// <summary>
    /// Palavra-passe fora do P05.
    /// </summary>
    public const string PasswordPolicy = "A palavra-passe tem de ter pelo menos 8 caracteres, uma letra e um algarismo.";

    /// <summary>
    /// Palavra-passe com mais de 128 caracteres (documentação da API v02, secção 8.1).
    /// </summary>
    public const string PasswordTooLong = "A palavra-passe tem no máximo 128 caracteres.";

    /// <summary>
    /// Palavra-passe atual que não coincide com a da conta, na alteração da palavra-passe (RF002, RF040, RF084).
    /// </summary>
    public const string CurrentIncorrect = "A palavra-passe atual está errada.";

    /// <summary>
    /// Condições de utilização não aceites (RF003, RF037).
    /// </summary>
    public const string AcceptTerms = "É obrigatório aceitar as condições de utilização.";

    /// <summary>
    /// Identificador fora da lista pré-definida (RF003, RF008, RF041, RF050).
    /// </summary>
    public const string NotInList = "O valor não pertence à lista pré-definida.";

    /// <summary>
    /// Nome com mais de 150 caracteres (coluna <c>candidate.full_name</c>).
    /// </summary>
    public const string FullNameTooLong = "O nome tem no máximo 150 caracteres.";

    /// <summary>
    /// Valor que não pode ser lido no tipo do campo (por exemplo, texto num campo booleano).
    /// </summary>
    public const string InvalidValue = "O valor não é válido.";

    /// <summary>
    /// Número de identificação fiscal sem exatamente 9 algarismos (P04).
    /// </summary>
    public const string TaxIdFormat = "O número de identificação fiscal tem de ter 9 algarismos.";

    /// <summary>
    /// Número de identificação fiscal já registado noutra Empresa (RF042).
    /// </summary>
    public const string TaxIdTaken = "O número de identificação fiscal já está registado.";

    /// <summary>
    /// Designação de uma competência ou de um benefício vazia (documentação da API v02, secção 4.3; RF099 a RF102).
    /// </summary>
    public const string ReferenceNameRequired = "A designação é obrigatória.";

    /// <summary>
    /// Designação que já existe na lista, sem distinção de maiúsculas (documentação da API v02, secção 4.3; RF099 a
    /// RF102).
    /// </summary>
    public const string ReferenceNameTaken = "A designação já existe na lista.";

    /// <summary>
    /// Data dos indicadores fora do formato <c>yyyy-MM-dd</c> (documentação da API v02, secções 2.3 e 6.12.1).
    /// </summary>
    public const string DateFormat = "A data tem de ter o formato aaaa-mm-dd.";

    /// <summary>
    /// Data inicial dos indicadores posterior à data final (documentação da API v02, secção 6.12.1).
    /// </summary>
    public const string PeriodStartAfterEnd = "A data inicial não pode ser posterior à data final.";

    /// <summary>
    /// Data final dos indicadores posterior à data atual (documentação da API v02, secção 6.12.1).
    /// </summary>
    public const string PeriodEndInFuture = "A data final não pode ser posterior à data atual.";

    /// <summary>
    /// Compõe o motivo de um texto acima do comprimento máximo da coluna, no formato do catálogo («O texto não pode
    /// exceder 1000 caracteres.», P08).
    /// </summary>
    /// <param name="maxLength">Comprimento máximo do campo.</param>
    /// <returns>O motivo, por exemplo «O texto não pode exceder 200 caracteres.».</returns>
    public static string TextTooLong(int maxLength)
    {
        return $"O texto não pode exceder {maxLength} caracteres.";
    }
}
