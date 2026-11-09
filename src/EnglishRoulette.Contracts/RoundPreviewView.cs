namespace EnglishRoulette.Contracts;

// A read-only sample of published content. No answer key, explanation or session state.
public sealed record RoundPreviewView(string StoryTitle, string SceneTitle, string Genre, string Difficulty,
    string NarrationExcerpt, string QuestionPrompt, IReadOnlyList<string> Options, string Language = "en");
