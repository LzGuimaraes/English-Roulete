using System.Text.Json;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Challenges;
using EnglishRoulette.Domain.Multiplayer;
using EnglishRoulette.Domain.Sessions;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.UnitTests;

public sealed class MultiplayerGameTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    private readonly LobbyMember[] _members = [Member("Host"), Member("Guest")];
    private static LobbyMember Member(string name) => new(Guid.NewGuid(), name, "🦊", true, Start, Start);
    private static int First(int count) => 0;
    private static int Last(int count) => count - 1;
    private IReadOnlyList<LobbyMember> Present(DateTimeOffset now) => _members.Select(member => member with { LastSeenUtc = now }).ToArray();
    private static StoryVersion Story()
    {
        var id = Guid.NewGuid();
        MultipleChoiceChallenge Challenge() => new(Guid.NewGuid(), "What color is the door?", [new("a", "Blue"), new("b", "Red"), new("c", "Green")], "a", "The door is blue.");
        return new(id, "Two doors", PublicationStatus.Published, "start",
        [new(id, "start", "A blue door opens.", Challenge(), false, [new("left", "Left", "left-end"), new("right", "Right", "right-end")]),
         new(id, "left-end", "The blue door leads left.", Challenge(), true, []),
         new(id, "right-end", "The blue door leads right.", Challenge(), true, [])]);
    }
    private MultiplayerMatch Create()
    {
        var story = Story();
        return MultiplayerGame.Create(story, story.Scenes.Select(scene => new SceneTiming(scene.Key, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(15))).ToArray(), _members, Start, Start.AddSeconds(5));
    }
    private MultiplayerMatch At(MultiplayerMatch match, double seconds) => MultiplayerGame.Advance(match, Present(Start.AddSeconds(seconds)), Start.AddSeconds(seconds), First);
    private MultiplayerMatch Send(MultiplayerMatch match, Guid actor, string kind, double seconds, string? key = null) =>
        MultiplayerGame.Execute(match, actor, new(Guid.NewGuid(), match.StateVersion, match.SceneInstanceId, kind, key), Present(Start.AddSeconds(seconds)), Start.AddSeconds(seconds), First);
    private MultiplayerMatch OpenQuestion()
    {
        var match = At(Create(), 5);
        match = Send(match, _members[0].PlayerId, "audio-ready", 5);
        match = Send(match, _members[1].PlayerId, "audio-ready", 5);
        return At(match, 10);
    }

    [Fact]
    public void Preparation_CountdownAndNarrationUseOnePersistedTimeline()
    {
        var match = Create();
        Assert.Equal(MultiplayerPhase.Roulette, At(match, 4).Phase);
        match = At(match, 5);
        var version = match.StateVersion;
        match = Send(match, _members[0].PlayerId, "audio-ready", 5);
        Assert.Equal(version, match.StateVersion); // independent acknowledgements commute
        match = Send(match, _members[1].PlayerId, "audio-ready", 5);
        Assert.Equal(MultiplayerPhase.Countdown, match.Phase);
        Assert.Equal(Start.AddSeconds(8), match.PhaseDeadlineUtc);
        Assert.Equal(MultiplayerPhase.Countdown, At(match, 7).Phase);
        match = At(match, 8);
        Assert.Equal(MultiplayerPhase.Narrating, match.Phase);
        Assert.Equal(Start.AddSeconds(8), match.NarrationStartedAtUtc);
        match = At(match, 11);
        Assert.Equal(MultiplayerPhase.QuestionOpen, match.Phase);
        Assert.Equal(Start.AddSeconds(25), match.PhaseDeadlineUtc); // no reset on late poll
    }

    [Fact]
    public void Scoring_UsesAnchoredTieWindows_AndCompetitionRanks()
    {
        var a = new MultiplayerAnswer(Guid.NewGuid(), "a", true, Start);
        var b = new MultiplayerAnswer(Guid.NewGuid(), "a", true, Start.AddMilliseconds(250));
        var c = new MultiplayerAnswer(Guid.NewGuid(), "a", true, Start.AddMilliseconds(500));
        var wrong = new MultiplayerAnswer(Guid.NewGuid(), "b", false, Start.AddMilliseconds(-100));
        var scored = MultiplayerScoring.Award([a, b, c, wrong], 300);
        Assert.Equal(new[] { 200, 200, 160, 0 }, scored.Select(answer => answer.Points));
    }

    [Fact]
    public void CorrectAnswer_IsNotScoredUntilAllRespond_AndReplayCannotScoreAgain()
    {
        var match = OpenQuestion();
        var request = new MultiplayerCommand(Guid.NewGuid(), match.StateVersion, match.SceneInstanceId, "answer", "a");
        match = MultiplayerGame.Execute(match, _members[0].PlayerId, request, Present(Start.AddSeconds(11)), Start.AddSeconds(11), First);
        Assert.Equal(MultiplayerPhase.QuestionOpen, match.Phase);
        Assert.All(match.Players, player => Assert.Equal(0, player.Score));
        match = Send(match, _members[1].PlayerId, "answer", 11.2, "a");
        Assert.Equal(MultiplayerPhase.QuestionReveal, match.Phase);
        Assert.All(match.Players, player => Assert.Equal(200, player.Score));
        var replay = MultiplayerGame.Execute(match, _members[0].PlayerId, request, Present(Start.AddSeconds(12)), Start.AddSeconds(12), First);
        Assert.Single(replay.Rounds);
        Assert.All(replay.Players, player => Assert.Equal(200, player.Score));
        Assert.Equal("IDEMPOTENCY_CONFLICT", Assert.Throws<GameRuleException>(() => MultiplayerGame.Execute(match, _members[0].PlayerId, request with { Key = "b" }, Present(Start), Start, First)).Code);
    }

    [Fact]
    public void Answers_RequireMembershipCurrentInstanceAndFirstAttempt()
    {
        var match = OpenQuestion();
        Assert.Equal("NOT_PARTICIPANT", Assert.Throws<GameRuleException>(() => Send(match, Guid.NewGuid(), "answer", 11, "a")).Code);
        Assert.Equal("INVALID_ANSWER", Assert.Throws<GameRuleException>(() => Send(match, _members[0].PlayerId, "answer", 11, "wrong")).Code);
        var command = new MultiplayerCommand(Guid.NewGuid(), match.StateVersion, Guid.NewGuid(), "answer", "a");
        Assert.Equal("INVALID_SCENE", Assert.Throws<GameRuleException>(() => MultiplayerGame.Execute(match, _members[0].PlayerId, command, Present(Start.AddSeconds(11)), Start.AddSeconds(11), First)).Code);
        match = Send(match, _members[0].PlayerId, "answer", 11, "b");
        Assert.Equal("ALREADY_ANSWERED", Assert.Throws<GameRuleException>(() => Send(match, _members[0].PlayerId, "answer", 12, "a")).Code);
    }

    [Fact]
    public void DeadlineClosesQuestionExactly_AndLateAnswerCannotBeAccepted()
    {
        var match = OpenQuestion();
        Assert.Throws<GameRuleException>(() => Send(match, _members[0].PlayerId, "answer", 25, "a"));
        match = At(match, 25);
        Assert.Equal(MultiplayerPhase.QuestionReveal, match.Phase);
        Assert.Empty(match.Rounds.Single().Answers);
        Assert.All(match.Players, player => Assert.Equal(0, player.Score));
    }

    [Fact]
    public void EqualVotes_DrawOnlyBetweenTiedChoices_AndReachTheSameEnding()
    {
        var match = OpenQuestion();
        match = Send(match, _members[0].PlayerId, "answer", 11, "a");
        match = Send(match, _members[1].PlayerId, "answer", 12, "b");
        match = At(match, 17);
        Assert.Equal(MultiplayerPhase.DecisionOpen, match.Phase);
        match = Send(match, _members[0].PlayerId, "vote", 18, "left");
        var vote = new MultiplayerCommand(Guid.NewGuid(), match.StateVersion, match.SceneInstanceId, "vote", "right");
        match = MultiplayerGame.Execute(match, _members[1].PlayerId, vote, Present(Start.AddSeconds(19)), Start.AddSeconds(19), Last);
        Assert.Equal("right-end", match.SceneKey);
        Assert.True(match.Decisions.Single().Tied);
        Assert.Equal(MultiplayerPhase.Preloading, match.Phase);
        match = Send(match, _members[0].PlayerId, "audio-ready", 20);
        match = Send(match, _members[1].PlayerId, "audio-ready", 20);
        match = At(match, 25);
        match = Send(match, _members[0].PlayerId, "answer", 26, "a");
        match = Send(match, _members[1].PlayerId, "answer", 26.1, "a");
        match = At(match, 32);
        Assert.Equal(MultiplayerPhase.StoryResult, match.Phase);
        Assert.Equal(2, match.Rounds.Count);
        Assert.Equal(new[] { "start", "right-end" }, match.VisitedScenes);
    }

    [Fact]
    public void NoVotes_UsesARecordedValidFallback()
    {
        var match = At(OpenQuestion(), 30);
        Assert.Equal(MultiplayerPhase.DecisionOpen, match.Phase);
        match = At(match, 46);
        Assert.True(match.Decisions.Single().NoVotes);
        Assert.Equal("left-end", match.SceneKey);
    }

    [Fact]
    public void MissingAudioCancelsClearly_AndLossOfQuorumNeverDeclaresWinner()
    {
        var match = At(Create(), 51);
        Assert.Equal(MultiplayerPhase.Cancelled, match.Phase);
        Assert.Contains("áudio", match.CancellationReason);
        match = MultiplayerGame.Advance(OpenQuestion(), [_members[0] with { LastSeenUtc = Start.AddSeconds(91) }], Start.AddSeconds(91), First);
        Assert.Equal(MultiplayerPhase.Cancelled, match.Phase);
        Assert.NotNull(match.CancellationReason);
    }

    [Fact]
    public void CheckpointRoundtrip_RetainsDeadlinesAnswersAndScores()
    {
        var match = Send(OpenQuestion(), _members[0].PlayerId, "answer", 11, "a");
        var restored = JsonSerializer.Deserialize<MultiplayerMatch>(JsonSerializer.Serialize(match))!;
        restored = At(restored, 25);
        Assert.Equal(MultiplayerPhase.QuestionReveal, restored.Phase);
        Assert.Equal(200, restored.Players[0].Score);
        Assert.Equal(0, restored.Players[1].Score);
        Assert.Equal(Start.AddSeconds(25), restored.Rounds.Single().ClosedAtUtc);
    }
    [Fact]
    public void EightCorrectAnswers_UseDocumentedRanks_AndTieBoundaryIsInclusive()
    {
        var answers = Enumerable.Range(0, 8).Select(index => new MultiplayerAnswer(Guid.NewGuid(), "a", true, Start.AddSeconds(index))).ToArray();
        Assert.Equal(new[] { 200, 180, 160, 140, 120, 100, 80, 60 }, MultiplayerScoring.Award(answers, 300).Select(answer => answer.Points));
        Assert.Equal(new[] { 200, 200, 160 }, MultiplayerScoring.Award([
            answers[0], answers[1] with { ReceivedAtUtc = Start.AddMilliseconds(300) },
            answers[2] with { ReceivedAtUtc = Start.AddMilliseconds(301) }], 300).Select(answer => answer.Points));
    }

    [Fact]
    public void Reconnection_DoesNotReopenAnExpiredQuestion_AndResumesBetweenScenes()
    {
        var match = OpenQuestion();
        IReadOnlyList<LobbyMember> OnlyHost(double seconds) => [_members[0] with { LastSeenUtc = Start.AddSeconds(seconds) }, _members[1] with { LastSeenUtc = Start.AddSeconds(-10) }];
        match = MultiplayerGame.Advance(match, OnlyHost(25), Start.AddSeconds(25), First);
        Assert.Equal(MultiplayerPhase.QuestionReveal, match.Phase);
        match = MultiplayerGame.Advance(match, OnlyHost(31), Start.AddSeconds(31), First);
        Assert.Equal(MultiplayerPhase.QuestionReveal, match.Phase); // wait between steps
        match = At(match, 40);
        Assert.Equal(MultiplayerPhase.DecisionOpen, match.Phase);
        Assert.Single(match.Rounds);
        Assert.Empty(match.Rounds[0].Answers);
        Assert.Equal(Start.AddSeconds(55), match.PhaseDeadlineUtc);
    }

    [Fact]
    public void CountdownWithoutQuorum_RepreparesInsteadOfSkippingAnUnheardScene()
    {
        var match = At(Create(), 5);
        match = Send(match, _members[0].PlayerId, "audio-ready", 5);
        match = Send(match, _members[1].PlayerId, "audio-ready", 5);
        match = MultiplayerGame.Advance(match, [_members[0] with { LastSeenUtc = Start.AddSeconds(35) }, _members[1]], Start.AddSeconds(35), First);
        Assert.Equal(MultiplayerPhase.Preloading, match.Phase);
        match = At(match, 36);
        Assert.Equal(MultiplayerPhase.Countdown, match.Phase);
        Assert.Equal(Start.AddSeconds(39), match.PhaseDeadlineUtc);
        Assert.Empty(match.Rounds);
    }

    [Fact]
    public void ReturningWithinGrace_GetsTimeToConfirmAudioAfterAnExpiredPreparation()
    {
        var match = At(Create(), 5);
        match = MultiplayerGame.Advance(match, [_members[0] with { LastSeenUtc = Start.AddSeconds(35) }, _members[1]], Start.AddSeconds(35), First);
        match = MultiplayerGame.Advance(match, [_members[0] with { LastSeenUtc = Start.AddSeconds(75) }, _members[1]], Start.AddSeconds(75), First);
        Assert.Equal(MultiplayerPhase.Preloading, match.Phase);
        match = At(match, 80);
        Assert.Equal(Start.AddSeconds(125), match.PhaseDeadlineUtc);
        match = Send(match, _members[0].PlayerId, "audio-ready", 81);
        match = Send(match, _members[1].PlayerId, "audio-ready", 82);
        Assert.Equal(MultiplayerPhase.Countdown, match.Phase);
        Assert.Empty(match.Rounds);
    }

}
