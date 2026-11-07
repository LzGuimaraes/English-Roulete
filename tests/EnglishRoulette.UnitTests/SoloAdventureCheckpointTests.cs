using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Sessions;

namespace EnglishRoulette.UnitTests;

public sealed class SoloAdventureCheckpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _playerId = Guid.NewGuid();

    [Fact]
    public void Restore_PreservesOpenQuestionIdentityAndDeadline()
    {
        var session = Create();
        session.Execute(_playerId, new StartSoloNarration(Guid.NewGuid(), 1), Now);
        var open = session.GetSnapshot(_playerId, Now.AddSeconds(30));

        var restored = SoloAdventureSession.Restore(session.Capture()).GetSnapshot(_playerId, Now.AddSeconds(35));

        Assert.Equal(open.Question?.InstanceId, restored.Question?.InstanceId);
        Assert.Equal(open.Question?.Prompt, restored.Question?.Prompt);
        Assert.Equal(open.Question?.Options, restored.Question?.Options);
        Assert.Equal(open.PhaseDeadlineUtc, restored.PhaseDeadlineUtc);
        Assert.Equal(open.StateVersion, restored.StateVersion);
        Assert.Equal(SoloAdventurePhase.QuestionOpen, restored.Phase);
    }

    [Fact]
    public void Restore_DoesNotGiveAnExpiredQuestionMoreTime()
    {
        var session = Create();
        session.Execute(_playerId, new StartSoloNarration(Guid.NewGuid(), 1), Now);

        var restored = SoloAdventureSession.Restore(session.Capture());
        var expired = restored.GetSnapshot(_playerId, Now.AddMinutes(2));

        Assert.True(expired.Answer?.TimedOut);
        Assert.Equal(Now.AddSeconds(45), expired.Answer?.ResolvedAtUtc);
    }

    [Fact]
    public void Restore_RetainsReceiptAndRejectsChangedReplay()
    {
        var session = Create();
        var command = new StartSoloNarration(Guid.NewGuid(), 1);
        var accepted = session.Execute(_playerId, command, Now);
        var restored = SoloAdventureSession.Restore(session.Capture());

        var replay = restored.Execute(_playerId, command, Now.AddHours(1));
        Assert.Equal(accepted.StateVersion, replay.StateVersion);
        Assert.Equal(accepted.Phase, replay.Phase);
        Assert.Equal(accepted.ServerNowUtc, replay.ServerNowUtc);
        var error = Assert.Throws<GameRuleException>(() => restored.Execute(_playerId,
            command with { ExpectedStateVersion = 2 }, Now));
        Assert.Equal("IDEMPOTENCY_CONFLICT", error.Code);
    }

    [Fact]
    public void Restore_RejectsInconsistentPhaseAndForeignReceipt()
    {
        var session = Create();
        session.Execute(_playerId, new StartSoloNarration(Guid.NewGuid(), 1), Now);
        var checkpoint = session.Capture();

        Assert.Throws<InvalidOperationException>(() => SoloAdventureSession.Restore(checkpoint with
        {
            Phase = SoloAdventurePhase.QuestionReveal
        }));
        Assert.Throws<InvalidOperationException>(() => SoloAdventureSession.Restore(checkpoint with
        {
            PlayerId = Guid.NewGuid()
        }));
    }

    private SoloAdventureSession Create()
    {
        var story = StoryFixture.Story();
        return new(Guid.NewGuid(), _playerId, story, story.Scenes.Select(scene =>
            new SceneTiming(scene.Key, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(15))));
    }
}
