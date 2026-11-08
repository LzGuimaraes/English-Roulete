using EnglishRoulette.Application.Admin;
using EnglishRoulette.Application.Presentation;
using EnglishRoulette.Contracts;
using EnglishRoulette.Contracts.Admin;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Api.Admin;

internal static class AdminContracts
{
    public static AdminStoryEditor View(CatalogDraft draft) => new()
    {
        Id = draft.Id,
        StoryId = draft.StoryId,
        Number = draft.Number,
        Status = draft.Status,
        Revision = draft.Revision,
        InitialSceneKey = draft.InitialSceneKey,
        Metadata = new()
        {
            Appearance = Appearance(draft.Metadata.Appearance ?? new()),
            Title = draft.Metadata.Title,
            Synopsis = draft.Metadata.Synopsis,
            Genre = draft.Metadata.Genre,
            Difficulty = draft.Metadata.Difficulty,
            Author = draft.Metadata.Author,
            LicenseType = draft.Metadata.LicenseType,
            LicenseEvidence = draft.Metadata.LicenseEvidence,
            Attribution = draft.Metadata.Attribution
        },
        Scenes = draft.Scenes.Select(scene => new AdminSceneEditor
        {
            Id = scene.Id,
            Key = scene.Key,
            Title = scene.Title,
            Narration = scene.Narration,
            IsEnding = scene.IsEnding,
            Vocabulary = (scene.Support?.Vocabulary ?? []).Select(h => new AdminVocabularyHint { Key = h.Key, Term = h.Term, Translation = h.Translation }).ToList(),
            Dialogue = (scene.Support?.Dialogue ?? []).Select(c => new AdminDialogueCue { Key = c.Key, Speaker = c.Speaker, Text = c.Text, StartMilliseconds = c.StartMilliseconds, EndMilliseconds = c.EndMilliseconds }).ToList(),
            Prompt = scene.Prompt,
            CorrectOptionKey = scene.CorrectOptionKey,
            Explanation = scene.Explanation,
            Options = scene.Options.Select(option => new AdminOptionEditor { Key = option.Key, Text = option.Text }).ToList(),
            Transitions = scene.Transitions.Select(transition => new AdminTransitionEditor
            { Key = transition.Key, Label = transition.Label, TargetSceneKey = transition.TargetSceneKey }).ToList(),
            Visual = scene.Visual is { } visual ? new(visual.Id, $"/api/admin/visuals/{visual.Id}", visual.AltText, visual.Animated, visual.Attribution, visual.LicenseEvidence) : null,
            Audio = scene.Audio is { } audio ? new(audio.Id, audio.DurationMs, $"/api/admin/media/{audio.Id}", audio.Attribution, audio.LicenseEvidence) : null
        }).ToList()
    };

    public static CatalogDraft Draft(AdminStoryEditor editor, Guid id)
    {
        if ((editor.Scenes ?? []).Any(scene => scene is null ||
            (scene.Vocabulary ?? []).Any(h => h is null) || (scene.Dialogue ?? []).Any(c => c is null) ||
            (scene.Options ?? []).Any(option => option is null) || (scene.Transitions ?? []).Any(transition => transition is null)))
        { throw new EnglishRoulette.Domain.GameRuleException("INVALID_CONTENT", "Revise os campos das cenas."); }
        var metadata = editor.Metadata ?? new();
        return new(id, editor.StoryId, editor.Number, editor.Status ?? "", editor.Revision, editor.InitialSceneKey ?? "",
            new(metadata.Title ?? "", metadata.Synopsis ?? "", metadata.Genre ?? "", metadata.Difficulty ?? "",
                metadata.Author ?? "", metadata.LicenseType ?? "", metadata.LicenseEvidence ?? "", metadata.Attribution ?? "", Appearance(metadata.Appearance ?? new())),
            (editor.Scenes ?? []).Select(scene => new CatalogScene(scene.Id, scene.Key ?? "", scene.Title ?? "", scene.Narration ?? "", scene.IsEnding,
                scene.Prompt ?? "", (scene.Options ?? []).Select(option => new CatalogOption(option.Key ?? "", option.Text ?? "")).ToArray(),
                scene.CorrectOptionKey ?? "", scene.Explanation ?? "", (scene.Transitions ?? []).Select(transition =>
                    new CatalogTransition(transition.Key ?? "", transition.Label ?? "", transition.TargetSceneKey ?? "")).ToArray(),
                scene.Audio is { } audio ? new(audio.Id, audio.DurationMs, audio.Attribution, audio.LicenseEvidence) : null,
                scene.Visual is { } visual ? new(visual.Id, visual.AltText, visual.Animated, visual.Attribution, visual.LicenseEvidence) : null, new SceneSupport
                {
                    Vocabulary = (scene.Vocabulary ?? []).Select(h => new VocabularyHint(h.Key ?? "", h.Term ?? "", h.Translation ?? "")).ToArray(),
                    Dialogue = (scene.Dialogue ?? []).Select(c => new DialogueCue(c.Key ?? "", c.Speaker ?? "", c.Text ?? "", c.StartMilliseconds, c.EndMilliseconds)).ToArray()
                })).ToArray());
    }

    public static StoryAppearance Appearance(CatalogAppearance value) => new()
    { Primary = value.Primary, Accent = value.Accent, Background = value.Background, Surface = value.Surface, Text = value.Text, Font = value.Font, Motion = value.Motion };
    private static CatalogAppearance Appearance(StoryAppearance value) => new()
    { Primary = value.Primary, Accent = value.Accent, Background = value.Background, Surface = value.Surface, Text = value.Text, Font = value.Font, Motion = value.Motion };

    public static AdminStorySummary Summary(CatalogSummary row) => new(row.Id, row.StoryId, row.Number,
        row.Title, row.Genre, row.Difficulty, row.Status, row.Revision, row.UpdatedAtUtc);
}
