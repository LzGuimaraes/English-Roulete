using EnglishRoulette.Application.Presentation;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Application.Admin;

public sealed record CatalogMetadata(string Title, string Synopsis, string Genre, string Difficulty,
    string Author, string LicenseType, string LicenseEvidence, string Attribution, CatalogAppearance? Appearance = null);
public sealed record CatalogOption(string Key, string Text);
public sealed record CatalogTransition(string Key, string Label, string TargetSceneKey);
public sealed record CatalogAudio(Guid Id, int DurationMs, string Attribution, string LicenseEvidence);
public sealed record CatalogVisual(Guid Id, string AltText, bool Animated, string Attribution, string LicenseEvidence);
public sealed record CatalogScene(Guid Id, string Key, string Title, string Narration, bool IsEnding,
    string Prompt, IReadOnlyList<CatalogOption> Options, string CorrectOptionKey, string Explanation,
    IReadOnlyList<CatalogTransition> Transitions, CatalogAudio? Audio, CatalogVisual? Visual = null);
public sealed record CatalogDraft(Guid Id, Guid StoryId, int Number, string Status, long Revision,
    string InitialSceneKey, CatalogMetadata Metadata, IReadOnlyList<CatalogScene> Scenes);
public sealed record CatalogSummary(Guid Id, Guid StoryId, int Number, string Title, string Genre,
    string Difficulty, string Status, long Revision, DateTimeOffset UpdatedAtUtc);
public sealed record PublicationCheck(IReadOnlyList<string> Issues)
{
    public bool CanPublish => Issues.Count == 0;
}
public sealed record PublishedAdventure(StoryVersion Story, CatalogMetadata Metadata,
    IReadOnlyDictionary<string, string> SceneTitles, IReadOnlyDictionary<string, CatalogAudio> Audio,
    IReadOnlyDictionary<string, CatalogVisual> Visuals);

public interface IAdminCatalog
{
    Task<IReadOnlyList<CatalogSummary>> ListAsync(int page, CancellationToken cancellationToken);
    Task<CatalogDraft?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<CatalogDraft> CreateAsync(Guid actorId, CancellationToken cancellationToken);
    Task<CatalogDraft> SaveAsync(CatalogDraft draft, Guid actorId, CancellationToken cancellationToken);
    Task<CatalogDraft> DuplicateAsync(Guid id, Guid actorId, CancellationToken cancellationToken);
    Task<PublicationCheck> ValidateAsync(Guid id, CancellationToken cancellationToken);
    Task PublishAsync(Guid id, long revision, bool reviewed, bool rightsConfirmed, Guid actorId, CancellationToken cancellationToken);
    Task ArchiveAsync(Guid id, long revision, Guid actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogSummary>> PublishedAsync(CancellationToken cancellationToken);
    Task<PublishedAdventure?> LoadPublishedAsync(Guid? id, CancellationToken cancellationToken);
    Task<PublishedAdventure?> LoadVersionAsync(Guid id, CancellationToken cancellationToken);
}
