using EnglishRoulette.Application.Solo;
using EnglishRoulette.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Infrastructure.Persistence;

public sealed class PostgresSoloSessionStore(AdventureDbContext db, TimeProvider timeProvider) : ISoloSessionStore
{
    public async Task<StoredSoloSession?> FindAsync(string credentialHash, CancellationToken cancellationToken)
    {
        var row = await db.SoloSessions.AsNoTracking().SingleOrDefaultAsync(
            session => session.CredentialHash == credentialHash, cancellationToken);
        return row is null ? null : new(CheckpointJson.Deserialize(row.CheckpointJson), row.Revision);
    }

    public async Task CreateAsync(string credentialHash, SoloAdventureCheckpoint checkpoint,
        string? previousCredentialHash, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.SoloSessions.Add(new()
        {
            CredentialHash = credentialHash,
            CheckpointJson = CheckpointJson.Serialize(checkpoint),
            Revision = 1,
            UpdatedAtUtc = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);
        if (previousCredentialHash is not null && previousCredentialHash != credentialHash)
        {
            await db.SoloSessions.Where(row => row.CredentialHash == previousCredentialHash)
                .ExecuteDeleteAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> TrySaveAsync(string credentialHash, long expectedRevision,
        SoloAdventureCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var json = CheckpointJson.Serialize(checkpoint);
        var now = timeProvider.GetUtcNow();
        // One SQL UPDATE commits state, answer, timing and receipts atomically.
        var changed = await db.SoloSessions
            .Where(row => row.CredentialHash == credentialHash && row.Revision == expectedRevision)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.CheckpointJson, json)
                .SetProperty(row => row.Revision, expectedRevision + 1).SetProperty(row => row.UpdatedAtUtc, now),
                cancellationToken);
        return changed == 1;
    }
}
