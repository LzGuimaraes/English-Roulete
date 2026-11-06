namespace EnglishRoulette.Contracts;

public sealed record OptionView(string Key, string Text);
public sealed record ChoiceView(string Key, string Label);
public sealed record QuestionView(Guid InstanceId, string Prompt, IReadOnlyList<OptionView> Options);
public sealed record AnswerView(string? OptionKey, bool IsCorrect, bool TimedOut, string Explanation);

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
    IReadOnlyList<ChoiceView> Choices);

public sealed record AdventureCommand(
    Guid RequestId,
    long ExpectedStateVersion,
    string Kind,
    Guid? QuestionInstanceId = null,
    string? OptionKey = null,
    string? TransitionKey = null);
