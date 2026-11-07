using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Sessions;

namespace EnglishRoulette.Application.Solo;

public sealed class PersistentSoloAdventure(ISoloSessionStore store, TimeProvider timeProvider)
{
    private const int MaximumSaveAttempts = 8;
    private readonly SoloAdventureCoordinator _coordinator = new(timeProvider);

    public Task<SoloAdventureSnapshot?> GetAsync(string credentialHash, CancellationToken cancellationToken) =>
        ApplyAsync(credentialHash, null, cancellationToken);

    public Task<SoloAdventureSnapshot?> ExecuteAsync(string credentialHash, SoloAdventureCommand command,
        CancellationToken cancellationToken) => ApplyAsync(credentialHash, command, cancellationToken);

    private async Task<SoloAdventureSnapshot?> ApplyAsync(string credentialHash, SoloAdventureCommand? command,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaximumSaveAttempts; attempt++)
        {
            var stored = await store.FindAsync(credentialHash, cancellationToken);
            if (stored is null) { return null; }
            var session = SoloAdventureSession.Restore(stored.Checkpoint);
            GameRuleException? rejected = null;
            SoloAdventureSnapshot? result = null;
            try
            {
                result = command is null
                    ? _coordinator.GetSnapshot(session, stored.Checkpoint.PlayerId)
                    : _coordinator.Execute(session, stored.Checkpoint.PlayerId, command);
            }
            catch (GameRuleException exception)
            {
                rejected = exception;
            }

            var updated = session.Capture();
            if (updated.StateVersion == stored.Checkpoint.StateVersion ||
                await store.TrySaveAsync(credentialHash, stored.Revision, updated, cancellationToken))
            {
                // Persist deadline transitions even when the incoming answer has been rejected.
                if (rejected is not null) { throw rejected; }
                return result;
            }
        }

        throw new GameRuleException("STATE_CONFLICT", "The session changed concurrently. Fetch its latest state.");
    }
}
