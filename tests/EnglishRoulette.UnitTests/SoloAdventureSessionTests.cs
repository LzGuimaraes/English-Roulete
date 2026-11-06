using System.Text.Json;
using EnglishRoulette.Application.Solo;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Sessions;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.UnitTests;

public sealed class SoloAdventureSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan NarrationDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan QuestionDuration = TimeSpan.FromSeconds(15);
    private readonly ManualTimeProvider _clock = new(Start);
    private readonly Guid _playerId = Guid.NewGuid();

    [Fact]
    public void Create_FixesStoryOwnerAndInitialScene()
    {
        var session = Create();
        var snapshot = Coordinator().GetSnapshot(session, _playerId);

        Assert.Equal(_playerId, snapshot.PlayerId);
        Assert.Equal(StoryFixture.VersionId, snapshot.StoryVersionId);
        Assert.Equal("platform", snapshot.SceneKey);
        Assert.Equal(SoloAdventurePhase.Preloading, snapshot.Phase);
        Assert.Equal(1, snapshot.StateVersion);
        Assert.Null(snapshot.Question);
        Assert.Null(snapshot.Answer);
        Assert.Null(snapshot.PhaseDeadlineUtc);
    }

    [Fact]
    public void Create_RejectsDraft()
    {
        AssertRule("CONTENT_NOT_PUBLISHED", () => Create(StoryFixture.Story(PublicationStatus.Draft)));
    }

    [Fact]
    public void StartNarration_UsesFrozenServerTimingAndHidesQuestion()
    {
        var snapshot = StartNarration(Create());

        Assert.Equal(SoloAdventurePhase.Narrating, snapshot.Phase);
        Assert.Equal(Start + NarrationDuration, snapshot.PhaseDeadlineUtc);
        Assert.Equal(Start, snapshot.ServerNowUtc);
        Assert.Null(snapshot.Question);
        Assert.Null(snapshot.Answer);
    }

    [Fact]
    public void GetSnapshot_OpensQuestionOnlyAtNarrationDeadline()
    {
        var session = Create();
        StartNarration(session);
        _clock.Advance(NarrationDuration - TimeSpan.FromTicks(1));
        Assert.Equal(SoloAdventurePhase.Narrating, Snapshot(session).Phase);

        _clock.Advance(TimeSpan.FromTicks(1));
        var open = Snapshot(session);

        Assert.Equal(SoloAdventurePhase.QuestionOpen, open.Phase);
        Assert.NotNull(open.Question);
        Assert.Equal(Start + NarrationDuration, open.Question.OpensAtUtc);
        Assert.Equal(Start + NarrationDuration + QuestionDuration, open.Question.ClosesAtUtc);
        Assert.Null(open.Answer);
    }

    [Fact]
    public void GetSnapshot_DelayedResumeDoesNotRestartQuestionTimer_Eng015_Eng016()
    {
        var session = Create();
        StartNarration(session);
        _clock.Advance(NarrationDuration + QuestionDuration + TimeSpan.FromMinutes(2));

        var resumed = Snapshot(session);

        Assert.Equal(SoloAdventurePhase.QuestionReveal, resumed.Phase);
        Assert.NotNull(resumed.Answer);
        Assert.True(resumed.Answer.TimedOut);
        Assert.False(resumed.Answer.IsCorrect);
        Assert.Null(resumed.Answer.OptionKey);
        Assert.Equal(Start + NarrationDuration + QuestionDuration, resumed.Answer.ResolvedAtUtc);
        Assert.Null(resumed.PhaseDeadlineUtc);
        Assert.Equal(resumed.StateVersion, Snapshot(session).StateVersion);
    }

    [Fact]
    public void GetSnapshot_ResumeDuringQuestionPreservesDeadlineAndVersion()
    {
        var session = Create();
        var open = OpenQuestion(session);
        _clock.Advance(TimeSpan.FromSeconds(5));

        var resumed = Snapshot(session);

        Assert.Equal(open.PhaseDeadlineUtc, resumed.PhaseDeadlineUtc);
        Assert.Equal(open.StateVersion, resumed.StateVersion);
        Assert.NotNull(resumed.Question);
        Assert.Equal(open.Question, resumed.Question);
        Assert.Null(resumed.Answer);
    }

    [Fact]
    public void GetSnapshot_OpenQuestionDoesNotSerializeAnswerKeyOrExplanation()
    {
        var open = OpenQuestion(Create());

        var json = JsonSerializer.Serialize(open);

        Assert.DoesNotContain("CorrectOptionKey", json);
        Assert.DoesNotContain("Explanation", json);
        Assert.Contains("\"Answer\":null", json);
    }

    [Fact]
    public void Execute_TimeTransitionMakesOldVersionStaleWithoutConsumingAnswer()
    {
        var session = Create();
        var started = StartNarration(session);
        _clock.Advance(NarrationDuration);

        AssertRule("STATE_CONFLICT", () => Execute(session,
            new ContinueSoloAdventure(Guid.NewGuid(), started.StateVersion)));

        var open = Snapshot(session);
        Assert.Equal(SoloAdventurePhase.QuestionOpen, open.Phase);
        Assert.Null(open.Answer);
        Assert.True(Submit(session, open, "a").Answer?.IsCorrect);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("b", false)]
    public void SubmitAnswer_ClosesQuestionAndRevealsFeedback(string option, bool correct)
    {
        var session = Create();
        var open = OpenQuestion(session);
        _clock.Advance(TimeSpan.FromSeconds(2));

        var result = Submit(session, open, option);

        Assert.Equal(SoloAdventurePhase.QuestionReveal, result.Phase);
        Assert.NotNull(result.Answer);
        Assert.Equal(correct, result.Answer.IsCorrect);
        Assert.False(result.Answer.TimedOut);
        Assert.Equal(option, result.Answer.OptionKey);
        Assert.Equal(Start + NarrationDuration + TimeSpan.FromSeconds(2), result.Answer.ResolvedAtUtc);
        Assert.Equal("Mia found a blue notebook beside the station clock.", result.Answer.Explanation);
        Assert.Equal(open.StateVersion + 1, result.StateVersion);
    }

    [Fact]
    public void SubmitAnswer_AllowsLastTickBeforeDeadline()
    {
        var session = Create();
        var open = OpenQuestion(session);
        _clock.Advance(QuestionDuration - TimeSpan.FromTicks(1));

        var result = Submit(session, open, "a");

        Assert.NotNull(result.Answer);
        Assert.True(result.Answer.IsCorrect);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SubmitAnswer_RejectsAtOrAfterDeadlineAndRecordsTimeout_Eng004(int extraTicks)
    {
        var session = Create();
        var open = OpenQuestion(session);
        _clock.Advance(QuestionDuration + TimeSpan.FromTicks(extraTicks));

        AssertRule("DEADLINE_EXPIRED", () => Submit(session, open, "a"));

        var result = Snapshot(session);
        Assert.Equal(SoloAdventurePhase.QuestionReveal, result.Phase);
        Assert.NotNull(result.Answer);
        Assert.True(result.Answer.TimedOut);
    }

    [Fact]
    public void SubmitAnswer_RejectsInvalidOptionWithoutConsumingAttempt()
    {
        var session = Create();
        var open = OpenQuestion(session);

        AssertRule("INVALID_ANSWER", () => Submit(session, open, "missing"));
        Assert.Equal(open.StateVersion, Snapshot(session).StateVersion);
        Assert.Null(Snapshot(session).Answer);
        Assert.NotNull(Submit(session, open, "a").Answer);
    }

    [Fact]
    public void SubmitAnswer_RejectsQuestionFromAnotherInstance()
    {
        var session = Create();
        var open = OpenQuestion(session);
        var command = new SubmitSoloAnswer(Guid.NewGuid(), open.StateVersion, Guid.NewGuid(), "a");

        AssertRule("INVALID_QUESTION", () => Execute(session, command));
        Assert.Null(Snapshot(session).Answer);
    }

    [Fact]
    public void SubmitAnswer_RejectsBeforeNarrationFinishes()
    {
        var session = Create();
        var narrating = StartNarration(session);
        var command = new SubmitSoloAnswer(Guid.NewGuid(), narrating.StateVersion, Guid.NewGuid(), "a");

        AssertRule("INVALID_GAME_PHASE", () => Execute(session, command));
        Assert.Equal(narrating.StateVersion, Snapshot(session).StateVersion);
    }

    [Fact]
    public void SubmitAnswer_ReplayReturnsOriginalReceiptEvenAfterAdvancing_Eng005()
    {
        var session = Create();
        var open = OpenQuestion(session);
        Assert.NotNull(open.Question);
        var command = new SubmitSoloAnswer(Guid.NewGuid(), open.StateVersion, open.Question.InstanceId, "a");
        var accepted = Execute(session, command);
        var next = Execute(session, new ContinueSoloAdventure(Guid.NewGuid(), accepted.StateVersion));
        _clock.Advance(TimeSpan.FromMinutes(1));

        var replay = Execute(session, command);

        Assert.Same(accepted, replay);
        Assert.Equal(next.StateVersion, Snapshot(session).StateVersion);
        Assert.Equal("notebook", Snapshot(session).SceneKey);
    }

    [Fact]
    public void SubmitAnswer_SameRequestWithChangedPayloadConflicts_Eng006()
    {
        var session = Create();
        var open = OpenQuestion(session);
        Assert.NotNull(open.Question);
        var command = new SubmitSoloAnswer(Guid.NewGuid(), open.StateVersion, open.Question.InstanceId, "b");
        var accepted = Execute(session, command);

        AssertRule("IDEMPOTENCY_CONFLICT", () => Execute(session, command with { OptionKey = "a" }));
        Assert.Equal(accepted.Answer, Snapshot(session).Answer);
    }

    [Fact]
    public void SubmitAnswer_WrongAnswerConsumesAttempt_Eng006()
    {
        var session = Create();
        var open = OpenQuestion(session);
        var accepted = Submit(session, open, "b");

        AssertRule("ALREADY_ANSWERED", () => Submit(session, accepted, "a"));
        Assert.NotNull(Snapshot(session).Answer);
        Assert.False(Snapshot(session).Answer?.IsCorrect);
    }

    [Fact]
    public void Execute_RejectsStaleStateWithoutMutation()
    {
        var session = Create();
        var command = new StartSoloNarration(Guid.NewGuid(), 1);
        var started = Execute(session, command);

        AssertRule("STATE_CONFLICT", () => Execute(session, new StartSoloNarration(Guid.NewGuid(), 1)));
        Assert.Equal(started.StateVersion, Snapshot(session).StateVersion);
    }

    [Fact]
    public void Execute_CannotReuseRequestIdForAnotherCommandType()
    {
        var session = Create();
        var request = Guid.NewGuid();
        var started = Execute(session, new StartSoloNarration(request, 1));

        AssertRule("IDEMPOTENCY_CONFLICT", () => Execute(session, new ContinueSoloAdventure(request, started.StateVersion)));
    }

    [Fact]
    public void Execute_ReplayMustPreserveExpectedVersion()
    {
        var session = Create();
        var command = new StartSoloNarration(Guid.NewGuid(), 1);
        var started = Execute(session, command);

        AssertRule("IDEMPOTENCY_CONFLICT", () => Execute(session,
            command with { ExpectedStateVersion = started.StateVersion }));
    }

    [Fact]
    public void Execute_RejectsUnauthorizedPlayerBeforeReplayOrTimeTransitions()
    {
        var session = Create();
        var command = new StartSoloNarration(Guid.NewGuid(), 1);
        Execute(session, command);
        _clock.Advance(NarrationDuration + QuestionDuration);
        var stranger = Guid.NewGuid();

        AssertRule("NOT_PARTICIPANT", () => Coordinator().Execute(session, stranger, command));
        AssertRule("NOT_PARTICIPANT", () => Coordinator().GetSnapshot(session, stranger));
    }

    [Fact]
    public void Continue_CannotSkipUnansweredQuestionOrNarration()
    {
        var session = Create();
        var narrating = StartNarration(session);
        AssertRule("INVALID_GAME_PHASE", () => Execute(session, new ContinueSoloAdventure(Guid.NewGuid(), narrating.StateVersion)));
        _clock.Advance(NarrationDuration);
        var open = Snapshot(session);
        AssertRule("INVALID_GAME_PHASE", () => Execute(session, new ContinueSoloAdventure(Guid.NewGuid(), open.StateVersion)));
    }

    [Fact]
    public void Continue_AfterTimeoutCanAdvanceWithoutSecondAttempt()
    {
        var session = Create();
        OpenQuestion(session);
        _clock.Advance(QuestionDuration);
        var expired = Snapshot(session);

        var next = Execute(session, new ContinueSoloAdventure(Guid.NewGuid(), expired.StateVersion));

        Assert.Equal("notebook", next.SceneKey);
        Assert.Equal(SoloAdventurePhase.Preloading, next.Phase);
        Assert.Null(next.Answer);
        Assert.Null(next.Question);
    }

    [Fact]
    public void Continue_FollowsThreeScenesAndStopsAtStoryResult()
    {
        var session = Create();
        var instances = new HashSet<Guid>();
        SoloAdventureSnapshot? result = null;
        foreach (var sceneKey in new[] { "platform", "notebook", "reunion" })
        {
            var open = OpenQuestion(session);
            Assert.Equal(sceneKey, open.SceneKey);
            Assert.NotNull(open.Question);
            Assert.True(instances.Add(open.Question.InstanceId));
            var revealed = Submit(session, open, "a");
            result = Execute(session, new ContinueSoloAdventure(Guid.NewGuid(), revealed.StateVersion));
        }

        Assert.NotNull(result);
        Assert.Equal(SoloAdventurePhase.StoryResult, result.Phase);
        AssertRule("INVALID_GAME_PHASE", () => Execute(session, new StartSoloNarration(Guid.NewGuid(), result.StateVersion)));
    }

    [Fact]
    public void ChoosePath_RequiresDecisionPhaseAndValidCurrentChoice()
    {
        var story = StoryFixture.WithScenes(
            StoryFixture.Scene("platform", transitions: [new("left", "Library", "library"), new("right", "Cafe", "cafe")]),
            StoryFixture.Scene("library", isEnding: true), StoryFixture.Scene("cafe", isEnding: true));
        var session = Create(story);
        var open = OpenQuestion(session);
        AssertRule("INVALID_GAME_PHASE", () => Execute(session, new ChooseSoloPath(Guid.NewGuid(), open.StateVersion, "left")));
        var revealed = Submit(session, open, "a");
        var decision = Execute(session, new ContinueSoloAdventure(Guid.NewGuid(), revealed.StateVersion));
        Assert.Equal(SoloAdventurePhase.DecisionOpen, decision.Phase);
        Assert.Equal(2, decision.Choices.Count);
        AssertRule("INVALID_CHOICE", () => Execute(session, new ChooseSoloPath(Guid.NewGuid(), decision.StateVersion, "missing")));

        var chosen = Execute(session, new ChooseSoloPath(Guid.NewGuid(), decision.StateVersion, "right"));

        Assert.Equal("cafe", chosen.SceneKey);
        Assert.Equal(SoloAdventurePhase.Preloading, chosen.Phase);
        Assert.Empty(chosen.Choices);
    }

    [Fact]
    public void SubmitAnswer_FromPreviousSceneCannotAnswerNextQuestion()
    {
        var session = Create();
        var first = OpenQuestion(session);
        Assert.NotNull(first.Question);
        var accepted = Submit(session, first, "a");
        Execute(session, new ContinueSoloAdventure(Guid.NewGuid(), accepted.StateVersion));
        var second = OpenQuestion(session);

        AssertRule("INVALID_QUESTION", () => Execute(session,
            new SubmitSoloAnswer(Guid.NewGuid(), second.StateVersion, first.Question.InstanceId, "a")));
        Assert.Null(Snapshot(session).Answer);
    }

    [Fact]
    public async Task Execute_ConcurrentDuplicateCommandsApplyOnce_Eng018()
    {
        var session = Create();
        var open = OpenQuestion(session);
        Assert.NotNull(open.Question);
        var command = new SubmitSoloAnswer(Guid.NewGuid(), open.StateVersion, open.Question.InstanceId, "a");

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Execute(session, command))));

        Assert.All(results, result => Assert.Same(results[0], result));
        Assert.Equal(open.StateVersion + 1, Snapshot(session).StateVersion);
    }

    [Fact]
    public async Task Execute_ConcurrentDifferentAnswersAcceptsOnlyOne()
    {
        var session = Create();
        var open = OpenQuestion(session);
        Assert.NotNull(open.Question);
        var outcomes = await Task.WhenAll(new[] { "a", "b" }.Select(option => Task.Run(() =>
        {
            var exception = Record.Exception(() => Execute(session,
                new SubmitSoloAnswer(Guid.NewGuid(), open.StateVersion, open.Question.InstanceId, option)));
            return exception;
        })));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is GameRuleException { Code: "ALREADY_ANSWERED" });
        Assert.Equal(open.StateVersion + 1, Snapshot(session).StateVersion);
    }

    [Fact]
    public void Create_CopiesTimingAndRejectsIncompleteOrInvalidConfiguration()
    {
        var story = StoryFixture.Story();
        var timing = Timings(story);
        var session = new SoloAdventureSession(Guid.NewGuid(), _playerId, story, timing);
        timing[0] = timing[0] with { NarrationDuration = TimeSpan.FromHours(1) };
        Assert.Equal(Start + NarrationDuration, StartNarration(session).PhaseDeadlineUtc);

        Assert.Throws<ArgumentException>(() => new SoloAdventureSession(Guid.NewGuid(), _playerId, story, []));
        Assert.Throws<ArgumentException>(() => new SoloAdventureSession(Guid.NewGuid(), _playerId, story, [timing[0], timing[0]]));
        timing = Timings(story);
        timing[0] = timing[0] with { QuestionDuration = TimeSpan.Zero };
        Assert.Throws<ArgumentException>(() => new SoloAdventureSession(Guid.NewGuid(), _playerId, story, timing));
    }

    [Fact]
    public void Execute_RejectsEmptyRequestIdWithoutMutation()
    {
        var session = Create();

        Assert.Throws<ArgumentException>(() => Execute(session, new StartSoloNarration(Guid.Empty, 1)));
        Assert.Equal(1, Snapshot(session).StateVersion);
    }

    private SoloAdventureCoordinator Coordinator() => new(_clock);
    private SoloAdventureSnapshot Snapshot(SoloAdventureSession session) => Coordinator().GetSnapshot(session, _playerId);
    private SoloAdventureSnapshot Execute(SoloAdventureSession session, SoloAdventureCommand command) =>
        Coordinator().Execute(session, _playerId, command);
    private SoloAdventureSession Create(StoryVersion? story = null)
    {
        story ??= StoryFixture.Story();
        return new SoloAdventureSession(Guid.NewGuid(), _playerId, story, Timings(story));
    }

    private static SceneTiming[] Timings(StoryVersion story) => story.Scenes
        .Select(scene => new SceneTiming(scene.Key, NarrationDuration, QuestionDuration)).ToArray();

    private SoloAdventureSnapshot StartNarration(SoloAdventureSession session) => Execute(session,
        new StartSoloNarration(Guid.NewGuid(), Snapshot(session).StateVersion));

    private SoloAdventureSnapshot OpenQuestion(SoloAdventureSession session)
    {
        StartNarration(session);
        _clock.Advance(NarrationDuration);
        return Snapshot(session);
    }

    private SoloAdventureSnapshot Submit(SoloAdventureSession session, SoloAdventureSnapshot snapshot, string option)
    {
        Assert.NotNull(snapshot.Question);
        return Execute(session, new SubmitSoloAnswer(Guid.NewGuid(), snapshot.StateVersion, snapshot.Question.InstanceId, option));
    }

    private static void AssertRule(string code, Action action) =>
        Assert.Equal(code, Assert.Throws<GameRuleException>(action).Code);

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
