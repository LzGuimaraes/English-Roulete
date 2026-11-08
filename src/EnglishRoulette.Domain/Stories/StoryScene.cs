using EnglishRoulette.Domain.Challenges;

namespace EnglishRoulette.Domain.Stories;

public sealed record SceneTransition(string Key, string Label, string TargetSceneKey);

public sealed class StoryScene
{
    public StoryScene(
        Guid storyVersionId,
        string key,
        string narration,
        MultipleChoiceChallenge challenge,
        bool isEnding,
        IEnumerable<SceneTransition> transitions,
        SceneSupport? support = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(narration);
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(transitions);

        if (storyVersionId == Guid.Empty)
        {
            throw new ArgumentException("A scene must belong to a story version.", nameof(storyVersionId));
        }

        var snapshot = transitions.ToArray();
        if (snapshot.Any(transition => transition is null ||
                string.IsNullOrWhiteSpace(transition.Key) ||
                string.IsNullOrWhiteSpace(transition.Label) ||
                string.IsNullOrWhiteSpace(transition.TargetSceneKey)) ||
            snapshot.Select(transition => transition.Key).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Transitions must have unique keys, labels and target scene keys.", nameof(transitions));
        }

        StoryVersionId = storyVersionId;
        Key = key;
        Narration = narration;
        Challenge = challenge;
        IsEnding = isEnding;
        Transitions = Array.AsReadOnly(snapshot);
        Support = support?.Freeze(narration);
    }

    public Guid StoryVersionId { get; }
    public string Key { get; }
    public string Narration { get; }
    public MultipleChoiceChallenge Challenge { get; }
    public bool IsEnding { get; }
    public IReadOnlyList<SceneTransition> Transitions { get; }
    public SceneSupport? Support { get; }
}
