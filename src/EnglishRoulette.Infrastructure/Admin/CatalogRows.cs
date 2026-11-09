namespace EnglishRoulette.Infrastructure.Admin;

public sealed class CatalogStoryRow
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CatalogVersionRow
{
    public Guid Id { get; set; }
    public Guid StoryId { get; set; }
    public int Number { get; set; }
    public string Status { get; set; } = "Draft";
    public long Revision { get; set; } = 1;
    public string Title { get; set; } = "Nova história";
    public string Synopsis { get; set; } = "";
    public string Genre { get; set; } = "Mistério";
    public string Difficulty { get; set; } = "A2";
    public string Language { get; set; } = "en";
    public string Author { get; set; } = "";
    public string LicenseType { get; set; } = "Original";
    public string LicenseEvidence { get; set; } = "";
    public string Attribution { get; set; } = "";
    public string AppearanceJson { get; set; } = "{}";
    public string InitialSceneKey { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public List<CatalogSceneRow> Scenes { get; set; } = [];
}

public sealed class CatalogSceneRow
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public int Position { get; set; }
    public string Narration { get; set; } = "";
    public string SupportJson { get; set; } = "{}";
    public bool IsEnding { get; set; }
    public string Prompt { get; set; } = "";
    public string CorrectOptionKey { get; set; } = "";
    public string Explanation { get; set; } = "";
    public Guid? VisualId { get; set; }
    public CatalogVisualRow? Visual { get; set; }
    public Guid? AudioId { get; set; }
    public CatalogMediaRow? Audio { get; set; }
    public List<CatalogOptionRow> Options { get; set; } = [];
    public List<CatalogTransitionRow> Transitions { get; set; } = [];
}

public sealed class CatalogOptionRow
{
    public Guid Id { get; set; }
    public Guid SceneId { get; set; }
    public string Key { get; set; } = "";
    public string Text { get; set; } = "";
    public int Position { get; set; }
}

public sealed class CatalogTransitionRow
{
    public Guid Id { get; set; }
    public Guid SceneId { get; set; }
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string TargetSceneKey { get; set; } = "";
    public int Position { get; set; }
}

public sealed class CatalogMediaRow
{
    public Guid Id { get; set; }
    public string StorageKey { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string NarrationSha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public int DurationMs { get; set; }
    public string Attribution { get; set; } = "";
    public string LicenseEvidence { get; set; } = "";
    public bool Published { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class AdminAuditRow
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Guid TargetId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset AtUtc { get; set; }
}

public sealed class CatalogVisualRow
{
    public Guid Id { get; set; }
    public string StorageKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Animated { get; set; }
    public string AltText { get; set; } = "";
    public string Attribution { get; set; } = "";
    public string LicenseEvidence { get; set; } = "";
    public bool Published { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
