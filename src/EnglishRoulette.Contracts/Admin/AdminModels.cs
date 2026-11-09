namespace EnglishRoulette.Contracts.Admin;

public sealed record AdminLoginRequest(string Email, string Password);
public sealed record AdminUserView(string Email);
public sealed record AntiforgeryView(string Token);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record RevisionRequest(long Revision);
public sealed record PublishRequest(long Revision, bool Reviewed, bool RightsConfirmed);
public sealed record PublicationCheckView(bool CanPublish, IReadOnlyList<string> Issues);
public sealed record AdminStorySummary(Guid Id, Guid StoryId, int Number, string Title, string Genre,
    string Difficulty, string Status, long Revision, DateTimeOffset UpdatedAtUtc, string Language = "en");
public sealed record PublicStoryView(Guid Id, string Title, string Synopsis, string Genre, string Difficulty, int SceneCount, StoryAppearance? Appearance = null, string Language = "en");

// Editorial DTOs are returned only by ADMIN-authorized endpoints. They are not game snapshots.
public sealed class AdminStoryEditor
{
    public Guid Id { get; set; }
    public Guid StoryId { get; set; }
    public int Number { get; set; }
    public string Status { get; set; } = "Draft";
    public long Revision { get; set; }
    public string InitialSceneKey { get; set; } = "";
    public AdminMetadata Metadata { get; set; } = new();
    public List<AdminSceneEditor> Scenes { get; set; } = [];
}
public sealed class AdminMetadata
{
    public StoryAppearance Appearance { get; set; } = new();
    public string Title { get; set; } = "";
    public string Synopsis { get; set; } = "";
    public string Genre { get; set; } = "Mistério";
    public string Difficulty { get; set; } = "A2";
    public string Language { get; set; } = "en";
    public string Author { get; set; } = "";
    public string LicenseType { get; set; } = "Original";
    public string LicenseEvidence { get; set; } = "";
    public string Attribution { get; set; } = "";
}
public sealed class AdminSceneEditor
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Narration { get; set; } = "";
    public bool IsEnding { get; set; }
    public string Prompt { get; set; } = "";
    public List<AdminOptionEditor> Options { get; set; } = [];
    public string CorrectOptionKey { get; set; } = "";
    public string Explanation { get; set; } = "";
    public List<AdminTransitionEditor> Transitions { get; set; } = [];
    public List<AdminVocabularyHint> Vocabulary { get; set; } = [];
    public List<AdminDialogueCue> Dialogue { get; set; } = [];
    public AdminVisualView? Visual { get; set; }
    public AdminAudioView? Audio { get; set; }
}
public sealed class AdminVocabularyHint
{
    public string Key { get; set; } = "";
    public string Term { get; set; } = "";
    public string Translation { get; set; } = "";
}
public sealed class AdminDialogueCue
{
    public string Key { get; set; } = "";
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    public int StartMilliseconds { get; set; }
    public int EndMilliseconds { get; set; }
}
public sealed class AdminOptionEditor
{
    public string Key { get; set; } = "";
    public string Text { get; set; } = "";
}
public sealed class AdminTransitionEditor
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string TargetSceneKey { get; set; } = "";
}
public sealed record AdminVisualView(Guid Id, string PreviewUrl, string AltText, bool Animated, string Attribution, string LicenseEvidence);
public sealed record AdminAudioView(Guid Id, int DurationMs, string PreviewUrl, string Attribution, string LicenseEvidence);

public sealed record PreviewSceneView(string Key, string Title, string Narration, bool IsEnding,
    string Prompt, IReadOnlyList<OptionView> Options, IReadOnlyList<ChoiceView> Choices, string? AudioUrl, SceneVisualView? Visual = null, StoryAppearance? Appearance = null, string Language = "en");
public sealed record PreviewAnswerRequest(long Revision, string SceneKey, string OptionKey);
public sealed record PreviewAnswerView(bool IsCorrect, string Explanation);
