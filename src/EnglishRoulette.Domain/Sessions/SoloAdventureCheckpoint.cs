using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Domain.Sessions;

public sealed record SoloCommandReceipt(SoloAdventureCommand Command, SoloAdventureSnapshot Snapshot);

// A durable aggregate checkpoint, including immutable content and accepted-command receipts.
// It contains answer keys and must remain exclusively on the server.
public sealed record SoloAdventureCheckpoint(
    Guid Id,
    Guid PlayerId,
    StoryDefinition Story,
    IReadOnlyList<SceneTiming> Timings,
    string SceneKey,
    SoloAdventurePhase Phase,
    long StateVersion,
    Guid QuestionInstanceId,
    DateTimeOffset QuestionOpensAtUtc,
    DateTimeOffset QuestionClosesAtUtc,
    SoloAnswerResult? Answer,
    IReadOnlyList<SoloCommandReceipt> Receipts);
