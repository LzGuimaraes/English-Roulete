using EnglishRoulette.Domain.Sessions;

namespace EnglishRoulette.Application.Solo;

public sealed record StoredSoloSession(SoloAdventureCheckpoint Checkpoint, long Revision);

public interface ISoloSessionStore
{
    Task<StoredSoloSession?> FindAsync(string credentialHash, CancellationToken cancellationToken);
    Task CreateAsync(string credentialHash, SoloAdventureCheckpoint checkpoint,
        string? previousCredentialHash, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(string credentialHash, long expectedRevision,
        SoloAdventureCheckpoint checkpoint, CancellationToken cancellationToken);
}
