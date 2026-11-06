using EnglishRoulette.Domain.Challenges;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.UnitTests;

internal static class StoryFixture
{
    public static readonly Guid VersionId = Guid.Parse("cf6bc1fb-7127-4739-b761-747f8d652b33");

    public static MultipleChoiceChallenge Challenge() => new(
        Guid.Parse("80d84ebd-ae79-48ac-90ed-a33fec4cf4ca"),
        "What did Mia find on the platform?",
        [new("a", "A blue notebook"), new("b", "A red umbrella"), new("c", "A train ticket")],
        "a",
        "Mia found a blue notebook beside the station clock.");

    public static StoryScene Scene(string key, bool isEnding = false, params SceneTransition[] transitions) => new(
        VersionId,
        key,
        "Mia found a blue notebook beside the station clock.",
        Challenge(),
        isEnding,
        transitions);

    public static StoryVersion Story(PublicationStatus status = PublicationStatus.Published) => new(
        VersionId,
        "The Notebook at Platform Three",
        status,
        "platform",
        [
            Scene("platform", transitions: [new("continue", "Look inside the notebook", "notebook")]),
            Scene("notebook", transitions: [new("continue", "Return the notebook", "reunion")]),
            Scene("reunion", isEnding: true)
        ]);

    public static StoryVersion WithScenes(params StoryScene[] scenes) => new(
        VersionId, "The Notebook at Platform Three", PublicationStatus.Published, "platform", scenes);
}
