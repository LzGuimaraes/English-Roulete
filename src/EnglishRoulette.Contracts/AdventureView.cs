namespace EnglishRoulette.Contracts;

public sealed record OptionView(string Key, string Text);
public sealed record ChoiceView(string Key, string Label);
public sealed record QuestionView(Guid InstanceId, string Prompt, IReadOnlyList<OptionView> Options);
public sealed record AnswerView(string? OptionKey, bool IsCorrect, bool TimedOut, string Explanation);
public sealed record AudioView(string Url, int DurationMilliseconds, int SafetyMarginMilliseconds);

public sealed record AdventureView(
    string Title,
    string SceneKey,
    string SceneTitle,
    int SceneNumber,
    string Narration,
    long StateVersion,
    string Phase,
    DateTimeOffset ServerNowUtc,
    DateTimeOffset? PhaseDeadlineUtc,
    QuestionView? Question,
    AnswerView? Answer,
    IReadOnlyList<ChoiceView> Choices,
    AudioView? Audio = null,
    IReadOnlyList<VisitedSceneView>? VisitedScenes = null,
    Guid? StoryVersionId = null,
    StoryAppearance? Appearance = null,
    SceneVisualView? Visual = null);

public sealed record AdventureCommand(
    Guid RequestId,
    long ExpectedStateVersion,
    string Kind,
    Guid? QuestionInstanceId = null,
    string? OptionKey = null,
    string? TransitionKey = null);

public sealed record VisitedSceneView(string Key, string Title);
public sealed record CreateAdventureRequest(Guid? StoryVersionId = null,
    StoryAppearance? Appearance = null,
    SceneVisualView? Visual = null);
