using EnglishRoulette.Domain.Challenges;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Domain.Sessions;

public enum SoloAdventurePhase
{
    Preloading,
    Narrating,
    QuestionOpen,
    QuestionReveal,
    DecisionOpen,
    StoryResult
}

// The server supplies these durations from validated media metadata and settings.
public sealed record SceneTiming(string SceneKey, TimeSpan NarrationDuration, TimeSpan QuestionDuration);

public sealed record SoloQuestion(
    Guid InstanceId,
    string Prompt,
    IReadOnlyList<AnswerOption> Options,
    DateTimeOffset OpensAtUtc,
    DateTimeOffset ClosesAtUtc);

public sealed record SoloAnswerResult(
    Guid QuestionInstanceId,
    string? OptionKey,
    bool IsCorrect,
    bool TimedOut,
    DateTimeOffset ResolvedAtUtc,
    string Explanation);

// An in-process view of the aggregate; future HTTP endpoints must map their own Contracts DTOs.
public sealed record SoloAdventureSnapshot(
    Guid SessionId,
    Guid PlayerId,
    Guid StoryVersionId,
    string StoryTitle,
    string SceneKey,
    string Narration,
    long StateVersion,
    SoloAdventurePhase Phase,
    DateTimeOffset ServerNowUtc,
    DateTimeOffset? PhaseDeadlineUtc,
    SoloQuestion? Question,
    SoloAnswerResult? Answer,
    IReadOnlyList<SceneTransition> Choices);
