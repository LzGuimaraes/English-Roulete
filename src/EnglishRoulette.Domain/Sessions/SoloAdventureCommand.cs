namespace EnglishRoulette.Domain.Sessions;

public abstract record SoloAdventureCommand(Guid RequestId, long ExpectedStateVersion);

public sealed record StartSoloNarration(Guid RequestId, long ExpectedStateVersion)
    : SoloAdventureCommand(RequestId, ExpectedStateVersion);

public sealed record SubmitSoloAnswer(
    Guid RequestId, long ExpectedStateVersion, Guid QuestionInstanceId, string OptionKey)
    : SoloAdventureCommand(RequestId, ExpectedStateVersion);

public sealed record ContinueSoloAdventure(Guid RequestId, long ExpectedStateVersion)
    : SoloAdventureCommand(RequestId, ExpectedStateVersion);

public sealed record ChooseSoloPath(Guid RequestId, long ExpectedStateVersion, string TransitionKey)
    : SoloAdventureCommand(RequestId, ExpectedStateVersion);

public sealed record UseSoloHint(Guid RequestId, long ExpectedStateVersion, Guid SceneInstanceId, string HintKey)
    : SoloAdventureCommand(RequestId, ExpectedStateVersion);
