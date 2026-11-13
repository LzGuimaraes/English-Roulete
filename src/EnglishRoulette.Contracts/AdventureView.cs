namespace EnglishRoulette.Contracts;

public sealed record OptionView(string Key, string Text);
public sealed record ChoiceView(string Key, string Label);
public sealed record QuestionView(Guid InstanceId, string Prompt, IReadOnlyList<OptionView> Options);
public sealed record AnswerView(string? OptionKey, bool IsCorrect, bool TimedOut, string Explanation, string? FullTranslation = null);
public sealed record AudioView(string Url, int DurationMilliseconds, int SafetyMarginMilliseconds);
public sealed record WordHintView(string Key, string Term, string? Translation);
public sealed record HintCostView(string PerWord, string Limit, int UsedCount, string CurrentDiscount, bool CanUse);
public sealed record PlayerScoreView(int Total, int? RoundBase, int? RoundDeduction, int? RoundPoints, string Rules);
public sealed record DialogueCueView(string Speaker, string Text, int StartMilliseconds, int EndMilliseconds);

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
    SceneVisualView? Visual = null,
    string Genre = "",
    IReadOnlyList<DialogueCueView>? Dialogue = null,
    Guid SceneInstanceId = default,
    IReadOnlyList<WordHintView>? WordHints = null, HintCostView? HintCost = null, PlayerScoreView? Score = null, string Language = "en");

public sealed record AdventureCommand(
    Guid RequestId,
    long ExpectedStateVersion,
    string Kind,
    Guid? QuestionInstanceId = null,
    string? OptionKey = null,
    string? TransitionKey = null, string? HintKey = null, Guid? SceneInstanceId = null);

public sealed record VisitedSceneView(string Key, string Title);
public sealed record CreateAdventureRequest(Guid? StoryVersionId = null,
    StoryAppearance? Appearance = null,
    SceneVisualView? Visual = null, string Language = "en", int QuestionSeconds = 20, string? Difficulty = null);
