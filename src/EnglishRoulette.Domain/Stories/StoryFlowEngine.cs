namespace EnglishRoulette.Domain.Stories;

public static class StoryFlowEngine
{
    public static StoryScene GetInitialScene(StoryVersion story)
    {
        ValidateForPlay(story);
        return story.Scenes.Single(scene => scene.Key == story.InitialSceneKey);
    }

    public static StoryScene? GetNextScene(StoryVersion story, string currentSceneKey, string? transitionKey = null)
    {
        ValidateForPlay(story);
        var scene = story.Scenes.SingleOrDefault(candidate => candidate.Key == currentSceneKey)
            ?? throw new GameRuleException("INVALID_SCENE", "The scene does not belong to this story version.");

        if (scene.IsEnding && transitionKey is null)
        {
            return null;
        }

        var transition = transitionKey is null && scene.Transitions.Count == 1
            ? scene.Transitions[0]
            : scene.Transitions.SingleOrDefault(candidate => candidate.Key == transitionKey);

        if (transition is null)
        {
            throw new GameRuleException("INVALID_CHOICE", "Select a transition available in the current scene.");
        }

        return story.Scenes.Single(candidate => candidate.Key == transition.TargetSceneKey);
    }

    public static void ValidateForPlay(StoryVersion story)
    {
        ArgumentNullException.ThrowIfNull(story);
        if (story.Status != PublicationStatus.Published)
        {
            throw new GameRuleException("CONTENT_NOT_PUBLISHED", "Only published story versions can be played.");
        }

        var scenes = story.Scenes.ToDictionary(scene => scene.Key, StringComparer.Ordinal);
        if (!scenes.ContainsKey(story.InitialSceneKey) || story.Scenes.Any(scene =>
                scene.StoryVersionId != story.Id ||
                scene.IsEnding != (scene.Transitions.Count == 0) ||
                scene.Transitions.Any(transition => !scenes.ContainsKey(transition.TargetSceneKey))))
        {
            throw InvalidGraph();
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(story.InitialSceneKey);
        while (pending.TryPop(out var key))
        {
            if (!visited.Add(key))
            {
                continue;
            }

            foreach (var transition in scenes[key].Transitions)
            {
                pending.Push(transition.TargetSceneKey);
            }
        }

        // Walk backwards from endings: every scene must offer a route to a final.
        var reachesEnding = story.Scenes.Where(scene => scene.IsEnding)
            .Select(scene => scene.Key).ToHashSet(StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (var scene in story.Scenes)
            {
                if (scene.Transitions.Any(transition => reachesEnding.Contains(transition.TargetSceneKey)))
                {
                    changed |= reachesEnding.Add(scene.Key);
                }
            }
        }
        while (changed);

        if (visited.Count != scenes.Count || reachesEnding.Count != scenes.Count)
        {
            throw InvalidGraph();
        }
    }

    private static GameRuleException InvalidGraph() => new(
        "INVALID_STORY_GRAPH", "Every scene must belong to this version, be reachable and lead to an ending.");
}
