namespace EnglishRoulette.Domain.Stories;

public enum PublicationStatus
{
    Draft,
    Published
}

public sealed class StoryVersion
{
    public StoryVersion(
        Guid id,
        string title,
        PublicationStatus status,
        string initialSceneKey,
        IEnumerable<StoryScene> scenes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialSceneKey);
        ArgumentNullException.ThrowIfNull(scenes);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("A story version must have an ID.", nameof(id));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        var snapshot = scenes.ToArray();
        if (snapshot.Any(scene => scene is null) ||
            snapshot.Select(scene => scene.Key).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Scenes must be non-null and have unique keys.", nameof(scenes));
        }

        Id = id;
        Title = title;
        Status = status;
        InitialSceneKey = initialSceneKey;
        Scenes = Array.AsReadOnly(snapshot);
    }

    public Guid Id { get; }
    public string Title { get; }
    public PublicationStatus Status { get; }
    public string InitialSceneKey { get; }
    public IReadOnlyList<StoryScene> Scenes { get; }
}
