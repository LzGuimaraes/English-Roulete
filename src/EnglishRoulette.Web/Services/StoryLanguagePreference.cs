namespace EnglishRoulette.Web.Services;

// A presentation preference only; no game credential or progress is persisted here.
public sealed class StoryLanguagePreference
{
    public string Value { get; set; } = "en";
    public static string Name(string language) => language == "pt-BR" ? "Português" : "Inglês";
}
