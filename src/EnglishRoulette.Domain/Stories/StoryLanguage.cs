namespace EnglishRoulette.Domain.Stories;

// Content language, independent of the interface language and editorial difficulty.
public static class StoryLanguage
{
    public const string English = "en";
    public const string Portuguese = "pt-BR";

    public static bool IsSupported(string? language) => language is English or Portuguese;

    public static string Require(string? language)
    {
        language ??= English;
        if (!IsSupported(language))
        { throw new GameRuleException("INVALID_LANGUAGE", "Escolha histórias em inglês ou português."); }
        return language;
    }
}
