using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Multiplayer;
using EnglishRoulette.Domain.Scoring;
using EnglishRoulette.Domain.Sessions;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.UnitTests;

public sealed class AssistedScoringTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly SoloScoringRules Rules = new(150, 125, 100, new(10, 50));
    private readonly Guid _player = Guid.NewGuid();
    private static StoryVersion Story() => StoryFixture.WithScenes(new StoryScene(StoryFixture.VersionId, "platform",
        "Mia found a blue notebook beside the station clock.", StoryFixture.Challenge(), true, [], new()
        { Vocabulary = [new("beside", "beside", "ao lado de"), new("notebook", "notebook", "caderno")] }));
    private static SceneTiming[] Timings() => [new("platform", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20))];
    private SoloAdventureSession Solo() => new(Guid.NewGuid(), _player, Story(), Timings(), Rules);

    [Theory]
    [InlineData(0, 150)]
    [InlineData(6, 150)]
    [InlineData(6.001, 125)]
    [InlineData(12, 125)]
    [InlineData(12.001, 100)]
    [InlineData(19.999, 100)]
    [InlineData(20, 0)]
    public void SoloPoints_UseServerTimeAndExactBoundary(double elapsed, int expected) =>
        Assert.Equal(expected, Rules.Award(true, Now, Now.AddSeconds(20), Now.AddSeconds(elapsed)));

    [Theory]
    [InlineData(150, 0, 0)]
    [InlineData(150, 1, 15)]
    [InlineData(125, 1, 13)]
    [InlineData(125, 2, 25)]
    [InlineData(150, 5, 75)]
    [InlineData(150, 12, 75)]
    [InlineData(0, 12, 0)]
    public void PercentageDiscount_RoundsUpCapsAndNeverChargesFromTotal(int points, int words, int expected) =>
        Assert.Equal(expected, Rules.Hints.Discount(points, words));

    [Fact]
    public void FixedCost_AndConfigurationAreValidated()
    {
        Assert.Equal(5, new AssistanceRules(0, 0, 10).Discount(5, 2));
        Assert.Equal(20, new AssistanceRules(0, 0, 10).Discount(100, 2));
        Assert.Throws<ArgumentException>(() => new AssistanceRules(10, 5).Validate());
        Assert.Throws<ArgumentException>(() => new AssistanceRules(10, 50, 10).Validate());
        Assert.Throws<ArgumentException>(() => new AssistanceRules(0, 50).Validate());
    }

    [Fact]
    public void SoloHints_RetryRefreshAndReopenChargeOnce_AndScoreOnce()
    {
        var session = Solo();
        var state = session.Execute(_player, new StartSoloNarration(Guid.NewGuid(), 1), Now);
        var use = new UseSoloHint(Guid.NewGuid(), state.StateVersion, state.SceneInstanceId, "beside");
        state = session.Execute(_player, use, Now.AddSeconds(2));
        session = SoloAdventureSession.Restore(session.Capture());
        var replay = session.Execute(_player, use, Now.AddSeconds(3));
        Assert.Equal(state.StateVersion, replay.StateVersion);
        Assert.Equal(state.ServerNowUtc, replay.ServerNowUtc);
        Assert.Equal(state.UsedHintKeys, replay.UsedHintKeys);
        state = session.Execute(_player, use with { RequestId = Guid.NewGuid(), ExpectedStateVersion = state.StateVersion }, Now.AddSeconds(4));
        Assert.Single(state.UsedHintKeys!);
        state = session.GetSnapshot(_player, Now.AddSeconds(10));
        var answer = new SubmitSoloAnswer(Guid.NewGuid(), state.StateVersion, state.Question!.InstanceId, "a");
        state = session.Execute(_player, answer, Now.AddSeconds(11));
        Assert.Equal(new SoloRoundScore(state.SceneInstanceId, "platform", true, false, 150, 1, 15, 135), Assert.Single(state.RoundScores!));
        session = SoloAdventureSession.Restore(session.Capture());
        Assert.Single(session.Execute(_player, answer, Now.AddSeconds(12)).RoundScores!);
        Assert.Equal(135, session.GetSnapshot(_player, Now.AddSeconds(13)).RoundScores!.Sum(round => round.Points));
        Assert.Equal("IDEMPOTENCY_CONFLICT", Assert.Throws<GameRuleException>(() => session.Execute(_player, use with { HintKey = "notebook" }, Now)).Code);
    }

    [Fact]
    public void SoloHints_RejectForeignSceneMissingWordAfterAnswerAndDeadline()
    {
        var session = Solo();
        var state = session.Execute(_player, new StartSoloNarration(Guid.NewGuid(), 1), Now);
        UseSoloHint Hint(string key, Guid? instance = null) => new(Guid.NewGuid(), state.StateVersion, instance ?? state.SceneInstanceId, key);
        Assert.Equal("INVALID_SCENE", Assert.Throws<GameRuleException>(() => session.Execute(_player, Hint("beside", Guid.NewGuid()), Now)).Code);
        Assert.Equal("INVALID_HINT", Assert.Throws<GameRuleException>(() => session.Execute(_player, Hint("absent"), Now)).Code);
        Assert.Equal("NOT_PARTICIPANT", Assert.Throws<GameRuleException>(() => session.Execute(Guid.NewGuid(), Hint("beside"), Now)).Code);
        state = session.GetSnapshot(_player, Now.AddSeconds(10));
        state = session.Execute(_player, new SubmitSoloAnswer(Guid.NewGuid(), state.StateVersion, state.SceneInstanceId, "b"), Now.AddSeconds(11));
        Assert.Equal("HINT_UNAVAILABLE", Assert.Throws<GameRuleException>(() => session.Execute(_player, Hint("beside"), Now.AddSeconds(11))).Code);
        Assert.Equal(0, Assert.Single(state.RoundScores!).Points);
        var expired = Solo();
        state = expired.Execute(_player, new StartSoloNarration(Guid.NewGuid(), 1), Now);
        Assert.Throws<GameRuleException>(() => expired.Execute(_player, Hint("beside"), Now.AddSeconds(30)));
        var timeout = Assert.Single(expired.GetSnapshot(_player, Now.AddSeconds(40)).RoundScores!);
        Assert.True(timeout.TimedOut);
        Assert.Equal(0, timeout.Points);
    }

    [Fact]
    public void LegacySoloCheckpoint_DoesNotInventHistoricalPoints()
    {
        var session = new SoloAdventureSession(Guid.NewGuid(), _player, Story(), Timings());
        var restored = SoloAdventureSession.Restore(session.Capture());
        Assert.Null(restored.GetSnapshot(_player, Now).ScoringRules);
        Assert.Empty(restored.GetSnapshot(_player, Now).RoundScores!);
    }

    [Fact]
    public void MultiplayerHints_PreservePhaseVersionTieRankAndOtherPlayersPoints()
    {
        var members = new[] { new LobbyMember(_player, "Host", "fox", true, Now, Now), new LobbyMember(Guid.NewGuid(), "Guest", "owl", true, Now, Now) };
        var match = MultiplayerGame.Create(Story(), Timings(), members, Now, Now, new(QuestionSeconds: 20, Hints: Rules.Hints));
        MultiplayerMatch Send(Guid actor, string kind, string? key, double second)
        {
            var at = Now.AddSeconds(second);
            return MultiplayerGame.Execute(match, actor, new(Guid.NewGuid(), match.StateVersion, match.SceneInstanceId, kind, key), members, at, _ => 0);
        }
        match = MultiplayerGame.Advance(match, members, Now, _ => 0);
        match = Send(_player, "audio-ready", null, 0);
        match = Send(members[1].PlayerId, "audio-ready", null, 0);
        match = MultiplayerGame.Advance(match, members, Now.AddSeconds(3), _ => 0);
        var version = match.StateVersion;
        match = Send(_player, "hint", "beside", 4);
        match = Send(_player, "hint", "beside", 5);
        Assert.Equal(version, match.StateVersion);
        Assert.Single(match.Hints!);
        match = MultiplayerGame.Advance(match, members, Now.AddSeconds(13), _ => 0);
        match = Send(_player, "answer", "a", 13);
        Assert.Equal("HINT_UNAVAILABLE", Assert.Throws<GameRuleException>(() => Send(_player, "hint", "notebook", 13.01)).Code);
        match = Send(members[1].PlayerId, "answer", "a", 13.1);
        Assert.Equal(new[] { 180, 200 }, match.Players.Select(player => player.Score));
        Assert.Equal(new[] { 200, 200 }, match.Answers.Select(answer => answer.BasePoints));
        Assert.Equal(20, match.Answers[0].HintDeduction);
        Assert.Single(match.Rounds);
        Assert.Equal(380, MultiplayerGame.Advance(match, members, Now.AddSeconds(14), _ => 0).Players.Sum(player => player.Score));
    }
}
