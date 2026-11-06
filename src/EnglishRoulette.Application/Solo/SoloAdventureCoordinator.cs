using EnglishRoulette.Domain.Sessions;

namespace EnglishRoulette.Application.Solo;

public sealed class SoloAdventureCoordinator(TimeProvider timeProvider)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public SoloAdventureSnapshot Execute(SoloAdventureSession session, Guid actorPlayerId, SoloAdventureCommand command)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Execute(actorPlayerId, command, _timeProvider.GetUtcNow());
    }

    public SoloAdventureSnapshot GetSnapshot(SoloAdventureSession session, Guid actorPlayerId)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.GetSnapshot(actorPlayerId, _timeProvider.GetUtcNow());
    }
}
