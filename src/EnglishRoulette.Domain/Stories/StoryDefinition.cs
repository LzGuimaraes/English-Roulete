using EnglishRoulette.Domain.Challenges;

namespace EnglishRoulette.Domain.Stories;

// Server-only content snapshots. Never reference these definitions from public Contracts.
public sealed record ChallengeDefinition(
    Guid Id, string Prompt, IReadOnlyList<AnswerOption> Options, string CorrectOptionKey, string Explanation);

public sealed record SceneDefinition(
    Guid StoryVersionId, string Key, string Narration, ChallengeDefinition Challenge,
    bool IsEnding, IReadOnlyList<SceneTransition> Transitions, SceneSupport? Support = null);

public sealed record StoryDefinition(
    Guid Id, string Title, PublicationStatus Status, string InitialSceneKey, IReadOnlyList<SceneDefinition> Scenes)
{
    public static StoryDefinition Capture(StoryVersion story) => new(story.Id, story.Title, story.Status,
        story.InitialSceneKey, Array.AsReadOnly(story.Scenes.Select(scene => new SceneDefinition(
            scene.StoryVersionId, scene.Key, scene.Narration,
            new(scene.Challenge.Id, scene.Challenge.Prompt, scene.Challenge.Options,
                scene.Challenge.CorrectOptionKey, scene.Challenge.Explanation),
            scene.IsEnding, scene.Transitions, scene.Support)).ToArray()));

    public StoryVersion Restore() => new(Id, Title, Status, InitialSceneKey, Scenes.Select(scene => new StoryScene(
        scene.StoryVersionId, scene.Key, scene.Narration,
        new(scene.Challenge.Id, scene.Challenge.Prompt, scene.Challenge.Options,
            scene.Challenge.CorrectOptionKey, scene.Challenge.Explanation), scene.IsEnding, scene.Transitions, scene.Support)));
}
