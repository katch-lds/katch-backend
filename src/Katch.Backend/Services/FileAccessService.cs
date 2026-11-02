// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Domain.Enums;
using Katch.Backend.Infrastructure.Files;
using Katch.Backend.Repositories;

namespace Katch.Backend.Services;

/// <summary>
/// Implementação de <see cref="IFileAccessService"/> (modelo de classes do backend v02, secções 5.1 e 5.3). Todas as
/// recusas dão o mesmo <see cref="ElementNotFoundException"/>.
/// </summary>
public sealed class FileAccessService : IFileAccessService
{
    private readonly IFileStorage fileStorage;
    private readonly ICandidateRepository candidates;
    private readonly IMatchRepository matches;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAccessService"/> class.
    /// </summary>
    /// <param name="fileStorage">Armazenamento dos ficheiros.</param>
    /// <param name="candidates">Repositório dos Candidatos.</param>
    /// <param name="matches">Repositório das respostas dos Candidatos às vagas.</param>
    public FileAccessService(IFileStorage fileStorage, ICandidateRepository candidates, IMatchRepository matches)
    {
        this.fileStorage = fileStorage;
        this.candidates = candidates;
        this.matches = matches;
    }

    /// <inheritdoc/>
    public async Task<FileContent> GetAsync(
        Guid userId,
        UserType userType,
        FileKind kind,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        // O curriculum vitae não tem URL nem passa por aqui (RNF007); um nome que não tenha a forma dos nomes gerados
        // pelo servidor (separadores, «..», caracteres inválidos) nunca chega ao sistema de ficheiros.
        if (kind == FileKind.CurriculumVitae || !LocalFileStorage.IsGeneratedName(fileName))
        {
            throw new ElementNotFoundException();
        }

        string path = $"{FileRules.Folder(kind)}/{fileName}";
        if (kind == FileKind.ProfilePhoto && !await this.CanGetProfilePhotoAsync(userId, userType, path, cancellationToken))
        {
            throw new ElementNotFoundException();
        }

        try
        {
            Stream content = await this.fileStorage.OpenReadAsync(path, cancellationToken);
            string contentType = FileRules.ContentType(FileRules.FromExtension(Path.GetExtension(fileName)));
            return new FileContent(content, contentType, fileName);
        }
        catch (FileNotFoundException)
        {
            throw new ElementNotFoundException();
        }
    }

    private async Task<bool> CanGetProfilePhotoAsync(Guid userId, UserType userType, string path, CancellationToken cancellationToken)
    {
        Candidate? owner = await this.candidates.GetByProfilePhotoPathAsync(path, cancellationToken);
        if (owner is null)
        {
            return false;
        }

        return userType switch
        {
            UserType.Candidate => owner.Id == userId,
            UserType.Recruiter => await this.matches.HasInterestFromCandidateInCompanyAsync(owner.Id, userId, cancellationToken),
            _ => false,
        };
    }
}
